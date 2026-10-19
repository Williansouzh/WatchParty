import 'package:flutter/foundation.dart';
import 'package:flutter_webrtc/flutter_webrtc.dart' show Helper;
import 'package:livekit_client/livekit_client.dart' as lk;

import '../../core/hub_client.dart';
import '../../core/models.dart';
import 'voice_chat.dart';

/// Voice call through a LiveKit server (enabled on the server with Voice:Provider=livekit).
/// The hub still tracks who is in the call and muted; LiveKit only carries the audio.
/// LiveKit identity = SignalR connection id, so speaking maps straight onto participants.
class LiveKitVoiceChat implements VoiceSession {
  LiveKitVoiceChat({required this.hub, required this.url, required this.onChanged});

  final HubClient hub;
  final String url;
  final VoidCallback onChanged;

  @override
  var speaking = <String>{};
  @override
  var active = false;
  @override
  var muted = false;

  lk.Room? _room;
  lk.EventsListener<lk.RoomEvent>? _listener;

  @override
  int get connectedPeers => _room?.remoteParticipants.length ?? 0;

  @override
  Future<void> join() async {
    final token = await hub.invoke('GetVoiceToken') as String;
    final room = lk.Room(
      roomOptions: const lk.RoomOptions(
        defaultAudioCaptureOptions: lk.AudioCaptureOptions(
          echoCancellation: true,
          noiseSuppression: true,
          autoGainControl: true,
        ),
      ),
    );
    _room = room;
    _listener = room.createListener()
      ..on<lk.ActiveSpeakersChangedEvent>((e) {
        speaking = e.speakers.map((p) => p is lk.LocalParticipant ? 'self' : p.identity).toSet();
        onChanged();
      })
      ..on<lk.ParticipantConnectedEvent>((_) => onChanged())
      ..on<lk.ParticipantDisconnectedEvent>((e) {
        speaking.remove(e.participant.identity);
        onChanged();
      });

    await room.connect(url, token);
    active = true;
    muted = false;
    await room.localParticipant?.setMicrophoneEnabled(true);
    if (!kIsWeb) {
      // Play the call through the loudspeaker, like the movie audio.
      await Helper.setSpeakerphoneOn(true).catchError((_) {});
    }
    await hub.invoke('JoinVoice');
    onChanged();
  }

  @override
  Future<void> leave({bool notifyServer = true}) async {
    final room = _room;
    _room = null;
    final wasActive = active;
    active = false;
    if (wasActive && notifyServer) await hub.invoke('LeaveVoice').catchError((_) => null);
    await _listener?.dispose();
    _listener = null;
    await room?.disconnect().catchError((_) {});
    await room?.dispose();
    speaking.clear();
    onChanged();
  }

  @override
  Future<void> setMuted(bool value) async {
    muted = value;
    onChanged();
    await _room?.localParticipant?.setMicrophoneEnabled(!value).catchError((_) => null);
    await hub.invoke('SetMuted', [value]).catchError((_) => null);
  }

  // LiveKit manages the connections itself; these only exist to match the mesh client.
  @override
  void onParticipantUpdated(Participant p) {}
  @override
  void onParticipantLeft(String connectionId) {}
  @override
  void onSignal(String from, String type, String payload) {}
}
