import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:flutter_webrtc/flutter_webrtc.dart';

import '../../core/hub_client.dart';
import '../../core/models.dart';

/// Voice call as a WebRTC mesh: everyone connects directly to everyone (great for ~6-8 friends).
/// SignalR only relays the handshake. Compatible with the web client (same message format).
class VoiceChat {
  VoiceChat({required this.hub, required this.iceServers, required this.onChanged});

  final HubClient hub;
  final List<Map<String, dynamic>> iceServers;
  final VoidCallback onChanged;

  /// Connection ids currently speaking; 'self' is the local user.
  final speaking = <String>{};
  var active = false;
  var muted = false;

  MediaStream? _local;
  final _peers = <String, _Peer>{};
  final _queues = <String, Future<void>>{};
  Timer? _levels;

  int get connectedPeers => _peers.values.where((p) => p.connected).length;

  Future<void> join() async {
    _local = await navigator.mediaDevices.getUserMedia({
      'audio': {'echoCancellation': true, 'noiseSuppression': true, 'autoGainControl': true},
      'video': false,
    });
    active = true;
    muted = false;
    if (!kIsWeb) {
      // Play the call through the loudspeaker, like the movie audio.
      await Helper.setSpeakerphoneOn(true).catchError((_) {});
    }

    final others = (await hub.invoke('JoinVoice') as List).map((e) => Participant.fromJson(asJson(e)));
    for (final other in others) {
      _enqueue(other.connectionId, () => _call(other.connectionId));
    }
    _levels = Timer.periodic(const Duration(milliseconds: 500), (_) => _pollLevels());
    onChanged();
  }

  Future<void> leave({bool notifyServer = true}) async {
    if (!active) return;
    active = false;
    _levels?.cancel();
    if (notifyServer) await hub.invoke('LeaveVoice').catchError((_) => null);
    for (final id in _peers.keys.toList()) {
      await _closePeer(id);
    }
    for (final track in _local?.getTracks() ?? <MediaStreamTrack>[]) {
      await track.stop();
    }
    await _local?.dispose();
    _local = null;
    speaking.clear();
    onChanged();
  }

  Future<void> setMuted(bool value) async {
    muted = value;
    for (final track in _local?.getAudioTracks() ?? <MediaStreamTrack>[]) {
      track.enabled = !value;
    }
    onChanged();
    await hub.invoke('SetMuted', [value]).catchError((_) => null);
  }

  // ----- events forwarded by the room -----

  void onParticipantUpdated(Participant p) {
    if (!p.inVoice) _enqueue(p.connectionId, () => _closePeer(p.connectionId));
  }

  void onParticipantLeft(String connectionId) => _enqueue(connectionId, () => _closePeer(connectionId));

  void onSignal(String from, String type, String payload) {
    if (!active) return;
    _enqueue(from, () => _handleSignal(from, type, jsonDecode(payload) as Map<String, dynamic>));
  }

  // ----- internals -----

  /// Signals for one peer must be handled in order (offer before its ICE candidates, etc.).
  void _enqueue(String connectionId, Future<void> Function() task) {
    _queues[connectionId] = (_queues[connectionId] ?? Future.value()).then((_) => task()).catchError((Object e) {
      debugPrint('voice: $e');
    });
  }

  Future<void> _handleSignal(String from, String type, Map<String, dynamic> data) async {
    final peer = _peers[from] ?? await _createPeer(from);
    switch (type) {
      case 'offer':
        await peer.pc.setRemoteDescription(RTCSessionDescription(data['sdp'] as String?, data['type'] as String?));
        await _flush(peer);
        final answer = await peer.pc.createAnswer();
        await peer.pc.setLocalDescription(answer);
        _send(from, 'answer', answer.toMap() as Map<String, dynamic>);
      case 'answer':
        await peer.pc.setRemoteDescription(RTCSessionDescription(data['sdp'] as String?, data['type'] as String?));
        await _flush(peer);
      case 'ice':
        final candidate = RTCIceCandidate(data['candidate'] as String?, data['sdpMid'] as String?, (data['sdpMLineIndex'] as num?)?.toInt());
        if (peer.hasRemote) {
          await peer.pc.addCandidate(candidate);
        } else {
          peer.pending.add(candidate);
        }
    }
  }

  Future<void> _call(String connectionId) async {
    final peer = await _createPeer(connectionId);
    final offer = await peer.pc.createOffer();
    await peer.pc.setLocalDescription(offer);
    _send(connectionId, 'offer', offer.toMap() as Map<String, dynamic>);
  }

  Future<_Peer> _createPeer(String connectionId) async {
    final pc = await createPeerConnection({'iceServers': iceServers, 'sdpSemantics': 'unified-plan'});
    final peer = _Peer(pc);
    _peers[connectionId] = peer;

    final local = _local;
    if (local != null) {
      for (final track in local.getTracks()) {
        await pc.addTrack(track, local);
      }
    }
    pc.onIceCandidate = (candidate) {
      if (candidate.candidate == null) return;
      _send(connectionId, 'ice', {
        'candidate': candidate.candidate,
        'sdpMid': candidate.sdpMid,
        'sdpMLineIndex': candidate.sdpMLineIndex,
      });
    };
    pc.onTrack = (event) async {
      // Native platforms play remote audio by themselves; the web needs an element.
      if (kIsWeb && event.streams.isNotEmpty) {
        final renderer = RTCVideoRenderer();
        await renderer.initialize();
        renderer.srcObject = event.streams.first;
        peer.renderer = renderer;
      }
    };
    pc.onConnectionState = (state) {
      peer.connected = state == RTCPeerConnectionState.RTCPeerConnectionStateConnected;
      if (state == RTCPeerConnectionState.RTCPeerConnectionStateFailed) {
        debugPrint('voice: connection with $connectionId failed — a TURN server may be needed');
      }
      onChanged();
    };
    return peer;
  }

  Future<void> _flush(_Peer peer) async {
    peer.hasRemote = true;
    for (final candidate in List.of(peer.pending)) {
      await peer.pc.addCandidate(candidate).catchError((_) {});
    }
    peer.pending.clear();
  }

  Future<void> _closePeer(String connectionId) async {
    final peer = _peers.remove(connectionId);
    if (peer == null) return;
    speaking.remove(connectionId);
    await peer.renderer?.dispose();
    await peer.pc.close();
    onChanged();
  }

  void _send(String to, String type, Map<String, dynamic> data) {
    hub.invoke('SendVoiceSignal', [to, type, jsonEncode(data)]).catchError((Object e) {
      debugPrint('voice signal: $e');
    });
  }

  /// Speaking indicators from WebRTC stats (audioLevel 0..1).
  Future<void> _pollLevels() async {
    if (!active) return;
    final before = Set.of(speaking);
    var selfLevel = 0.0;
    for (final entry in _peers.entries) {
      try {
        final stats = await entry.value.pc.getStats();
        var remoteLevel = 0.0;
        for (final report in stats) {
          final level = (report.values['audioLevel'] as num?)?.toDouble() ?? 0;
          if (report.type == 'inbound-rtp' && report.values['kind'] == 'audio') remoteLevel = level;
          if (report.type == 'media-source' && report.values['kind'] == 'audio' && level > selfLevel) selfLevel = level;
        }
        remoteLevel > 0.02 ? speaking.add(entry.key) : speaking.remove(entry.key);
      } catch (_) {/* stats are cosmetic */}
    }
    !muted && selfLevel > 0.02 ? speaking.add('self') : speaking.remove('self');
    if (!setEquals(before, speaking)) onChanged();
  }
}

class _Peer {
  _Peer(this.pc);

  final RTCPeerConnection pc;
  final pending = <RTCIceCandidate>[];
  var hasRemote = false;
  var connected = false;
  RTCVideoRenderer? renderer;
}
