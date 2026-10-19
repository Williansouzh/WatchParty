import 'package:flutter/foundation.dart';
import 'package:flutter_local_notifications/flutter_local_notifications.dart';

/// System notifications ("Ana começou a assistir…"). Tapping one opens the room.
class Notifications {
  Notifications._();

  static final _plugin = FlutterLocalNotificationsPlugin();
  static var _ready = false;
  static void Function(String roomCode)? _onOpenRoom;

  static Future<void> init({required void Function(String roomCode) onOpenRoom}) async {
    _onOpenRoom = onOpenRoom;
    if (kIsWeb) return; // the web build shows in-app alerts only
    try {
      _ready = await _plugin.initialize(
            settings: const InitializationSettings(
              android: AndroidInitializationSettings('@mipmap/ic_launcher'),
              iOS: DarwinInitializationSettings(requestAlertPermission: false),
            ),
            onDidReceiveNotificationResponse: (response) {
              final code = response.payload;
              if (code != null && code.isNotEmpty) _onOpenRoom?.call(code);
            },
          ) ??
          false;
    } catch (e) {
      debugPrint('notifications unavailable: $e');
    }
  }

  /// Android 13+ and iOS ask the user once.
  static Future<void> requestPermission() async {
    if (!_ready) return;
    await _plugin
        .resolvePlatformSpecificImplementation<AndroidFlutterLocalNotificationsPlugin>()
        ?.requestNotificationsPermission();
    await _plugin
        .resolvePlatformSpecificImplementation<IOSFlutterLocalNotificationsPlugin>()
        ?.requestPermissions(alert: true, sound: true);
  }

  static Future<void> roomActivity({required String code, required String text}) async {
    if (!_ready) return;
    await _plugin.show(
      id: code.hashCode,
      title: 'WatchParty',
      body: text,
      payload: code,
      notificationDetails: const NotificationDetails(
        android: AndroidNotificationDetails(
          'rooms',
          'Salas',
          channelDescription: 'Quando seus amigos criam uma sala ou começam a assistir',
          importance: Importance.high,
          priority: Priority.high,
        ),
        iOS: DarwinNotificationDetails(),
      ),
    );
  }
}
