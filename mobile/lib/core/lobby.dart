import 'dart:async';

import 'hub_client.dart';
import 'models.dart';
import 'session.dart';

class RoomActivity {
  const RoomActivity({required this.kind, required this.code, required this.roomName, required this.videoTitle,
      required this.byUserId, required this.byDisplayName});

  factory RoomActivity.fromJson(Json j) => RoomActivity(
        kind: j['kind'] as String,
        code: j['code'] as String,
        roomName: j['roomName'] as String,
        videoTitle: j['videoTitle'] as String?,
        byUserId: j['byUserId'] as String,
        byDisplayName: j['byDisplayName'] as String,
      );

  final String kind; // created | started
  final String code;
  final String roomName;
  final String? videoTitle;
  final String byUserId;
  final String byDisplayName;

  String get text => kind == 'created'
      ? '$byDisplayName criou a sala "$roomName"'
      : '$byDisplayName começou a assistir${videoTitle == null ? '' : ' "$videoTitle"'}';
}

/// Background connection while signed in: hears when friends create or start rooms.
class Lobby {
  Lobby(this.session);

  final Session session;
  HubClient? _hub;
  final _activity = StreamController<RoomActivity>.broadcast();

  Stream<RoomActivity> get activity => _activity.stream;

  Future<void> start() async {
    if (_hub != null || session.serverUrl == null) return;
    final hub = HubClient(url: Uri.parse('${session.serverUrl}/hubs/room'), accessToken: () => session.token ?? '');
    _hub = hub;
    hub.on('RoomActivity', (args) {
      final activity = RoomActivity.fromJson(asJson(args[0]));
      if (activity.byUserId != session.user?.id) _activity.add(activity);
    });
    hub.onReconnected = () async {
      await hub.invoke('WatchLobby');
    };
    try {
      await hub.start();
      await hub.invoke('WatchLobby');
    } catch (_) {
      _hub = null; // alerts are a nice-to-have
    }
  }

  Future<void> stop() async {
    final hub = _hub;
    _hub = null;
    await hub?.stop();
  }
}
