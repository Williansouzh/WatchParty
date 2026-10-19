import 'dart:async';
import 'dart:math' as math;

import 'package:flutter/foundation.dart';
import 'package:video_player/video_player.dart';

import '../../core/api.dart';
import '../../core/format.dart';
import '../../core/hub_client.dart';
import '../../core/models.dart';
import '../../core/session.dart';
import 'voice_chat.dart';
import 'voice_livekit.dart';

enum RoomStatus { connecting, connected, reconnecting, failed, deleted }

sealed class ChatEntry {}

class MessageEntry extends ChatEntry {
  MessageEntry(this.message);

  final ChatMessage message;
}

class SystemEntry extends ChatEntry {
  SystemEntry(this.text);

  final String text;
}

class Reaction {
  Reaction(this.emoji, this.displayName) : key = UniqueKey();

  final String emoji;
  final String displayName;
  final Key key;
}

/// Everything happening in a room: connection, synced playback, queue, chat, people, reactions and voice.
///
/// The server stores "position P at server time T, playing or paused"; every device computes where the
/// video should be *now* and corrects itself (seek for big gaps, a slightly faster/slower speed for small ones).
/// If someone gets stuck loading, the room pauses for everyone and resumes when they're ready.
class RoomController extends ChangeNotifier {
  RoomController({required this.code, required this.session, required this.api}) {
    hub = HubClient(url: Uri.parse('${session.serverUrl}/hubs/room'), accessToken: () => session.token ?? '');
  }

  static const reactions = ['😂', '😍', '🔥', '😱', '👏', '😭', '👍', '🍿'];
  static const _applySeekThreshold = 0.6;
  static const _hardSeekThreshold = 2.0;
  static const _softSyncThreshold = 0.35;
  static const _bufferingReportDelay = Duration(milliseconds: 2500);
  static const _loadGrace = Duration(seconds: 8);
  static const _actionText = {'Play': 'deu play', 'Pause': 'pausou', 'Seek': 'pulou para'};

  final String code;
  final Session session;
  final Api api;
  late final HubClient hub;

  var status = RoomStatus.connecting;
  String? error;
  Room? room;
  String? myConnectionId;
  final participants = <String, Participant>{};
  final chat = <ChatEntry>[];
  var queue = <QueueItem>[];
  var waitingFor = <String>[];
  var unread = 0;
  var chatVisible = true;

  VideoPlayerController? video;
  var videoLoading = false;
  String? videoError;
  var needsTap = false; // autoplay blocked (web)
  SubtitleInfo? subtitle;
  VoiceSession? voice;

  final _toasts = StreamController<String>.broadcast();
  final _reactions = StreamController<Reaction>.broadcast();
  Stream<String> get toasts => _toasts.stream;
  Stream<Reaction> get reactionStream => _reactions.stream;

  PlaybackState? _state;
  String? _loadedVideoId;
  DateTime _loadedAt = DateTime.now();
  var _clockOffsetMs = 0.0;
  var _pendingLocal = 0;
  var _lastLocal = DateTime.fromMillisecondsSinceEpoch(0);
  Timer? _drift;
  Timer? _bufferingTimer;
  var _bufferingReported = false;
  var _endReported = false;
  RtcConfig _rtc = const RtcConfig();
  var _disposed = false;

  int get watchingCount => participants.values.map((p) => p.userId).toSet().length;
  List<Participant> get voiceParticipants => participants.values.where((p) => p.inVoice).toList();
  bool get isHost => room?.hostId == session.user?.id;
  bool get canControl => !(room?.hostOnlyControl ?? false) || isHost;
  List<SubtitleInfo> get subtitles => room?.currentVideo?.subtitles ?? const [];

  bool isSpeaking(String connectionId) {
    final v = voice;
    if (v == null) return false;
    return v.speaking.contains(connectionId == myConnectionId ? 'self' : connectionId);
  }

  // ---------- Lifecycle ----------

  Future<void> start() async {
    _registerHandlers();
    hub.onReconnecting = () {
      status = RoomStatus.reconnecting;
      _system('Conexão instável, reconectando…');
    };
    hub.onReconnected = () async {
      if (voice?.active ?? false) {
        await voice!.leave(notifyServer: false);
        voice = null;
        _toast('A call caiu. Entre de novo na voz.');
      }
      await _join();
      status = RoomStatus.connected;
      _system('Reconectado ✓');
    };
    hub.onClosed = () {
      status = RoomStatus.failed;
      error = 'Desconectado do servidor.';
      _notify();
    };

    try {
      _rtc = await api.rtcConfig().catchError((_) => const RtcConfig());
      await hub.start();
      await _join();
      status = RoomStatus.connected;
    } on HubException catch (e) {
      status = RoomStatus.failed;
      error = e.message;
    } catch (_) {
      status = RoomStatus.failed;
      error = 'Não deu para conectar na sala.';
    }
    _drift = Timer.periodic(const Duration(seconds: 4), (_) => _checkDrift());
    _notify();
  }

  @override
  Future<void> dispose() async {
    _disposed = true;
    _drift?.cancel();
    _bufferingTimer?.cancel();
    await voice?.leave().catchError((_) {});
    await hub.invoke('LeaveRoom').catchError((_) => null);
    await hub.stop();
    await _disposeVideo();
    await _toasts.close();
    await _reactions.close();
    super.dispose();
  }

  Future<void> _join() async {
    final response = asJson(await hub.invoke('JoinRoom', [code]));
    room = Room.fromJson(asJson(response['room']));
    queue = room!.queue;
    waitingFor = List<String>.from(response['waitingFor'] as List? ?? const []);
    myConnectionId = response['connectionId'] as String;
    participants
      ..clear()
      ..addEntries((response['participants'] as List).map((e) {
        final p = Participant.fromJson(asJson(e));
        return MapEntry(p.connectionId, p);
      }));
    chat
      ..clear()
      ..addAll((response['messages'] as List).map((e) => MessageEntry(ChatMessage.fromJson(asJson(e)))))
      ..add(SystemEntry('Você entrou. Tudo que alguém fizer no player acontece pra todo mundo.'));
    await _calibrate((response['serverTimeMs'] as num).toInt());
    await _loadVideo(room!.currentVideo, room!.playback);
    _notify();
  }

  void _registerHandlers() {
    hub.on('ParticipantJoined', (args) {
      final p = Participant.fromJson(asJson(args[0]));
      participants[p.connectionId] = p;
      _system('${p.displayName} entrou na sala');
    });
    hub.on('ParticipantLeft', (args) {
      final id = args[0] as String;
      final p = participants.remove(id);
      voice?.onParticipantLeft(id);
      if (p != null) _system('${p.displayName} saiu');
    });
    hub.on('ParticipantUpdated', (args) {
      final p = Participant.fromJson(asJson(args[0]));
      final before = participants[p.connectionId];
      participants[p.connectionId] = p;
      voice?.onParticipantUpdated(p);
      if (before != null && before.inVoice != p.inVoice) {
        _system('${p.displayName} ${p.inVoice ? 'entrou na' : 'saiu da'} call');
      } else {
        _notify();
      }
    });
    hub.on('PlaybackChanged', (args) {
      final event = asJson(args[0]);
      final state = PlaybackState.fromJson(asJson(event['state']));
      _apply(state);
      switch (event['reason']) {
        case 'buffering':
          _system('⏸ Pausado para esperar quem está carregando');
        case 'resumed':
          _system('▶ Todo mundo pronto, continuando');
        default:
          _system('${event['byDisplayName']} ${_actionText[event['action']] ?? ''} ${formatClock(state.positionSeconds)}');
      }
    });
    hub.on('VideoChanged', (args) {
      final event = asJson(args[0]);
      final changed = Video.fromJson(asJson(event['video']));
      queue = parseQueue(event['queue']);
      _loadVideo(changed, PlaybackState.fromJson(asJson(event['state'])));
      _system(event['reason'] == 'next'
          ? '▶ Próximo da fila: "${changed.title}"'
          : '${event['byDisplayName']} colocou "${changed.title}"');
    });
    hub.on('VideoRemoved', (args) {
      final event = asJson(args[0]);
      _loadVideo(null, PlaybackState.fromJson(asJson(event['state'])));
      _system('O vídeo foi apagado da biblioteca por quem enviou.');
      _toast('O vídeo desta sala foi apagado.');
    });
    hub.on('QueueChanged', (args) {
      queue = parseQueue(args[0]);
      _notify();
    });
    hub.on('RoomSettingsChanged', (args) {
      final settings = asJson(args[0]);
      final hostOnly = settings['hostOnlyControl'] as bool;
      room = room?.copyWith(hostOnlyControl: hostOnly);
      _system(hostOnly
          ? '🔒 ${settings['byDisplayName']} deixou só o anfitrião controlar o vídeo'
          : '🔓 Todo mundo pode controlar o vídeo');
    });
    hub.on('BufferingChanged', (args) {
      waitingFor = List<String>.from(args[0] as List);
      _notify();
    });
    hub.on('Reaction', (args) {
      final r = asJson(args[0]);
      if (!_reactions.isClosed) _reactions.add(Reaction(r['emoji'] as String, r['displayName'] as String));
    });
    hub.on('ChatMessage', (args) {
      chat.add(MessageEntry(ChatMessage.fromJson(asJson(args[0]))));
      if (!chatVisible) unread++;
      _notify();
    });
    hub.on('VoiceSignal', (args) => voice?.onSignal(args[0] as String, args[1] as String, args[2] as String));
    hub.on('RoomDeleted', (_) {
      status = RoomStatus.deleted;
      _notify();
    });
  }

  // ---------- Playback sync ----------

  /// Estimates the server clock offset; keeps the fastest of a few round trips (least noise).
  Future<void> _calibrate(int serverTimeMs) async {
    _clockOffsetMs = (serverTimeMs - DateTime.now().millisecondsSinceEpoch).toDouble();
    var best = double.infinity;
    for (var i = 0; i < 4; i++) {
      final t0 = DateTime.now().millisecondsSinceEpoch;
      final server = (await hub.invoke('GetServerTime') as num).toDouble();
      final t1 = DateTime.now().millisecondsSinceEpoch;
      if (t1 - t0 < best) {
        best = (t1 - t0).toDouble();
        _clockOffsetMs = server - (t0 + t1) / 2;
      }
    }
  }

  double _serverNowMs() => DateTime.now().millisecondsSinceEpoch + _clockOffsetMs;

  double _expectedPosition(PlaybackState s) {
    final elapsed = s.isPlaying ? math.max(0, (_serverNowMs() - s.updatedAtMs) / 1000) : 0;
    var position = s.positionSeconds + elapsed;
    final duration = video?.value.duration;
    if (duration != null && duration > Duration.zero) position = math.min(position, duration.inMilliseconds / 1000);
    return position;
  }

  double get _currentPosition => (video?.value.position.inMilliseconds ?? 0) / 1000;

  /// HLS (adaptive quality) on phones when the server has it; MP4 otherwise and on the web build.
  VideoPlayerController _createController(Video v) {
    final options = VideoPlayerOptions(allowBackgroundPlayback: true); // keeps playing in picture-in-picture
    if (v.hasHls && !kIsWeb) {
      return VideoPlayerController.networkUrl(
        Uri.parse(session.hlsUrl(v.id)),
        formatHint: VideoFormat.hls,
        httpHeaders: session.authHeaders,
        videoPlayerOptions: options,
      );
    }
    return VideoPlayerController.networkUrl(Uri.parse(session.videoStreamUrl(v.id)), videoPlayerOptions: options);
  }

  Future<void> _loadVideo(Video? v, PlaybackState state) async {
    room = room?.withVideo(v);
    _state = state;
    if (v == null || !v.isReady) {
      await _disposeVideo();
      _notify();
      return;
    }
    if (_loadedVideoId == v.id && video != null) {
      await _apply(state, force: true);
      return;
    }

    await _disposeVideo();
    _loadedVideoId = v.id;
    _loadedAt = DateTime.now();
    _endReported = false;
    subtitle = null;
    videoLoading = true;
    videoError = null;
    _notify();

    final controller = _createController(v);
    video = controller;
    try {
      await controller.initialize();
    } catch (_) {
      if (video == controller) {
        videoError = 'Não foi possível carregar o vídeo.';
        videoLoading = false;
        _notify();
      }
      return;
    }
    if (video != controller || _disposed) return; // switched while loading
    controller.addListener(_onVideoTick);
    videoLoading = false;
    await _apply(_state!, force: true);
    _notify();
  }

  Future<void> _disposeVideo() async {
    final old = video;
    video = null;
    _loadedVideoId = null;
    videoError = null;
    videoLoading = false;
    _bufferingTimer?.cancel();
    if (_bufferingReported) _reportBuffering(false);
    old?.removeListener(_onVideoTick);
    await old?.dispose();
  }

  /// Watches the player for two things: getting stuck loading, and reaching the end.
  void _onVideoTick() {
    final c = video;
    if (c == null || !c.value.isInitialized) return;
    final value = c.value;

    final stuck = value.isBuffering && (_state?.isPlaying ?? false);
    if (stuck && !_bufferingReported && !(_bufferingTimer?.isActive ?? false)) {
      _scheduleBufferingReport(_bufferingReportDelay);
    } else if (!value.isBuffering) {
      _bufferingTimer?.cancel();
      if (_bufferingReported) _reportBuffering(false);
    }

    final ended = value.duration > Duration.zero &&
        value.position >= value.duration - const Duration(milliseconds: 300) &&
        !value.isPlaying;
    if (ended && !_endReported && _loadedVideoId != null) {
      _endReported = true;
      // Every player reports the end; the server advances the queue only once.
      hub.invoke('VideoEnded', [_loadedVideoId]).catchError((_) => null);
    }
  }

  void _scheduleBufferingReport(Duration delay) {
    _bufferingTimer?.cancel();
    _bufferingTimer = Timer(delay, () {
      final c = video;
      if (c == null || !c.value.isBuffering || !(_state?.isPlaying ?? false) || _bufferingReported) return;
      // Right after loading a video everyone loads a bit: check again when the grace period ends.
      final sinceLoad = DateTime.now().difference(_loadedAt);
      if (sinceLoad < _loadGrace) {
        _scheduleBufferingReport(_loadGrace - sinceLoad + const Duration(milliseconds: 100));
      } else {
        _reportBuffering(true);
      }
    });
  }

  void _reportBuffering(bool buffering) {
    _bufferingReported = buffering;
    hub.invoke('SetBuffering', [buffering]).catchError((_) => null);
  }

  /// Applies a state that came from the server.
  Future<void> _apply(PlaybackState incoming, {bool force = false}) async {
    var s = incoming;
    // Never go back to an older state (a remote event that lost the race against our own action).
    final current = _state;
    if (!force && current != null && s.videoId == current.videoId && s.updatedAtMs < current.updatedAtMs) s = current;
    _state = s;

    final c = video;
    if (c == null || !c.value.isInitialized) return;
    final target = _expectedPosition(s);
    await c.setPlaybackSpeed(1);
    if (force || (_currentPosition - target).abs() > _applySeekThreshold) {
      await c.seekTo(Duration(milliseconds: (target * 1000).round()));
      if (target < (c.value.duration.inMilliseconds / 1000) - 1) _endReported = false;
    }
    if (s.isPlaying && !c.value.isPlaying) {
      await _play(c);
    } else if (!s.isPlaying && c.value.isPlaying) {
      await c.pause();
    }
  }

  Future<void> _play(VideoPlayerController c) async {
    try {
      await c.play();
      needsTap = false;
    } catch (_) {
      needsTap = true; // browser autoplay policy
      _notify();
    }
  }

  /// Called from the "tap to join" overlay (a user gesture) when autoplay was blocked.
  Future<void> resumeFromTap() async {
    needsTap = false;
    final s = _state;
    if (s != null) await _apply(s, force: true);
    _notify();
  }

  /// Re-applies the room state, e.g. when the app returns from background.
  Future<void> resync() async {
    try {
      final fresh = PlaybackState.fromJson(asJson(await hub.invoke('GetPlayback')));
      await _apply(fresh, force: true);
    } catch (_) {}
  }

  Future<void> togglePlay() async {
    final c = video;
    if (c == null) return;
    await _local(c.value.isPlaying ? 'Pause' : 'Play');
  }

  Future<void> seekTo(Duration position) => _local('Seek', position: position);

  Future<void> seekBy(int seconds) async {
    final c = video;
    if (c == null) return;
    final target = c.value.position + Duration(seconds: seconds);
    final clamped = target < Duration.zero ? Duration.zero : (target > c.value.duration ? c.value.duration : target);
    await seekTo(clamped);
  }

  Future<void> _local(String action, {Duration? position}) async {
    final c = video;
    if (c == null || !c.value.isInitialized || _state?.videoId == null) return;
    if (!canControl) {
      _toast('Nesta sala só o anfitrião controla o vídeo.');
      return;
    }
    _pendingLocal++;
    _lastLocal = DateTime.now();
    try {
      if (action == 'Play') await _play(c);
      if (action == 'Pause') await c.pause();
      if (position != null) {
        await c.seekTo(position);
        _endReported = false;
      }
      final seconds = (position ?? c.value.position).inMilliseconds / 1000;
      _state = PlaybackState.fromJson(asJson(await hub.invoke('Playback', [action, seconds])));
    } on HubException catch (e) {
      _toast(e.message);
      // The server refused (e.g. host-only room): go back to where the room is.
      final s = _state;
      if (s != null) await _apply(s, force: true);
    } finally {
      _pendingLocal--;
      _notify();
    }
  }

  Future<void> _checkDrift() async {
    final c = video;
    final s = _state;
    if (status != RoomStatus.connected || _pendingLocal > 0 || c == null || s?.videoId == null) return;
    if (!c.value.isInitialized || c.value.isBuffering) return;

    final started = DateTime.now();
    final PlaybackState fresh;
    try {
      fresh = PlaybackState.fromJson(asJson(await hub.invoke('GetPlayback')));
    } catch (_) {
      return;
    }
    // Our own action happened while the request was in flight: this answer is stale.
    if (_pendingLocal > 0 || _lastLocal.isAfter(started)) return;
    final current = _state;
    if (current != null && fresh.videoId == current.videoId && fresh.updatedAtMs < current.updatedAtMs) return;
    _state = fresh;

    if (fresh.isPlaying != c.value.isPlaying) {
      if (!needsTap) await _apply(fresh);
      return;
    }
    if (!fresh.isPlaying) return;

    final drift = _expectedPosition(fresh) - _currentPosition; // positive = we are behind
    if (drift.abs() > _hardSeekThreshold) {
      await c.seekTo(Duration(milliseconds: (_expectedPosition(fresh) * 1000).round()));
      await c.setPlaybackSpeed(1);
    } else if (drift.abs() > _softSyncThreshold) {
      await c.setPlaybackSpeed(drift > 0 ? 1.06 : 0.94);
    } else {
      await c.setPlaybackSpeed(1);
    }
  }

  // ---------- Subtitles (per person, not synced) ----------

  Future<void> selectSubtitle(SubtitleInfo? choice) async {
    final c = video;
    final v = room?.currentVideo;
    if (c == null || v == null) return;
    subtitle = choice;
    _notify();
    if (choice == null) {
      await c.setClosedCaptionFile(null);
      return;
    }
    try {
      final text = await api.subtitleText(v.id, choice.id);
      await c.setClosedCaptionFile(Future.value(WebVTTCaptionFile(text)));
    } on ApiException catch (e) {
      subtitle = null;
      _toast(e.message);
      _notify();
    }
  }

  // ---------- Room actions ----------

  Future<void> changeVideo(String videoId) async {
    try {
      final changed = asJson(await hub.invoke('ChangeVideo', [videoId]));
      final v = Video.fromJson(asJson(changed['video']));
      queue = parseQueue(changed['queue']);
      await _loadVideo(v, PlaybackState.fromJson(asJson(changed['playback'])));
      _system('Você colocou "${v.title}"');
    } on HubException catch (e) {
      _toast(e.message);
    }
  }

  Future<void> addToQueue(String videoId) => _queueCall('AddToQueue', [videoId], 'Adicionado à fila');

  Future<void> addPlaylistToQueue(String playlistId) => _queueCall('AddPlaylistToQueue', [playlistId], 'Playlist adicionada à fila');

  Future<void> removeFromQueue(String itemId) => _queueCall('RemoveFromQueue', [itemId], null);

  Future<void> _queueCall(String method, List<Object?> args, String? success) async {
    try {
      queue = parseQueue(await hub.invoke(method, args));
      if (success != null) _toast(success);
      _notify();
    } on HubException catch (e) {
      _toast(e.message);
    }
  }

  Future<void> playNext() async {
    try {
      await hub.invoke('PlayNext');
    } on HubException catch (e) {
      _toast(e.message);
    }
  }

  Future<void> setHostOnly(bool hostOnly) async {
    try {
      await hub.invoke('SetHostOnlyControl', [hostOnly]);
    } on HubException catch (e) {
      _toast(e.message);
    }
  }

  void sendReaction(String emoji) => hub.invoke('SendReaction', [emoji]).catchError((_) => null);

  Future<bool> sendMessage(String content) async {
    try {
      await hub.invoke('SendMessage', [content]);
      return true;
    } on HubException catch (e) {
      _toast(e.message);
      return false;
    }
  }

  Future<void> deleteRoom() async {
    try {
      await api.deleteRoom(code);
      status = RoomStatus.deleted;
      _notify();
    } on ApiException catch (e) {
      _toast(e.message);
    }
  }

  void setChatVisible(bool visible) {
    chatVisible = visible;
    if (visible) unread = 0;
    _notify();
  }

  // ---------- Voice ----------

  Future<void> joinVoice() async {
    if (voice?.active ?? false) return;
    final liveKitUrl = _rtc.liveKitUrl;
    final v = liveKitUrl != null
        ? LiveKitVoiceChat(hub: hub, url: liveKitUrl, onChanged: _notify)
        : VoiceChat(hub: hub, iceServers: _rtc.iceServers, onChanged: _notify);
    voice = v;
    try {
      await v.join();
      _updateSelf(inVoice: true, isMuted: false);
      _toast('Você está na call. Use fone para evitar eco 🎧');
    } catch (e) {
      await v.leave(notifyServer: false).catchError((_) {});
      voice = null;
      _toast('Não foi possível usar o microfone. Verifique a permissão.');
      _notify();
    }
  }

  Future<void> leaveVoice() async {
    await voice?.leave();
    voice = null;
    _updateSelf(inVoice: false, isMuted: false);
  }

  Future<void> toggleMute() async {
    final v = voice;
    if (v == null) return;
    await v.setMuted(!v.muted);
    _updateSelf(isMuted: v.muted);
  }

  /// The server only tells the *others* about our own changes, so mirror them locally.
  void _updateSelf({bool? inVoice, bool? isMuted}) {
    final me = participants[myConnectionId];
    if (me != null) participants[me.connectionId] = me.copyWith(inVoice: inVoice, isMuted: isMuted);
    _notify();
  }

  // ---------- helpers ----------

  void _system(String text) {
    chat.add(SystemEntry(text));
    _notify();
  }

  void _toast(String message) {
    if (!_toasts.isClosed) _toasts.add(message);
  }

  void _notify() {
    if (!_disposed) notifyListeners();
  }
}
