import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'models.dart';

/// Which server we talk to, the access token and the signed-in user. Persisted between launches.
class Session extends ChangeNotifier {
  Session._(this._prefs);

  static const _serverKey = 'server_url';
  static const _tokenKey = 'access_token';

  /// Can be baked in at build time: flutter build apk --dart-define=SERVER_URL=https://...
  static const _defaultServer = String.fromEnvironment('SERVER_URL');

  final SharedPreferences _prefs;

  String? serverUrl;
  String? token;
  AppUser? user;

  bool get isSignedIn => user != null && token != null;

  static Future<Session> load() async {
    final prefs = await SharedPreferences.getInstance();
    final session = Session._(prefs);
    session.serverUrl = prefs.getString(_serverKey) ??
        (_defaultServer.isNotEmpty ? _defaultServer : (kIsWeb ? Uri.base.origin : null));
    session.token = prefs.getString(_tokenKey);
    return session;
  }

  static String normalizeServer(String url) {
    var value = url.trim();
    if (!value.startsWith('http://') && !value.startsWith('https://')) value = 'http://$value';
    while (value.endsWith('/')) {
      value = value.substring(0, value.length - 1);
    }
    return value;
  }

  Future<void> setServer(String url) async {
    serverUrl = normalizeServer(url);
    await _prefs.setString(_serverKey, serverUrl!);
    notifyListeners();
  }

  Future<void> signIn(String accessToken, AppUser signedInUser) async {
    token = accessToken;
    user = signedInUser;
    await _prefs.setString(_tokenKey, accessToken);
    notifyListeners();
  }

  void restore(AppUser signedInUser) {
    user = signedInUser;
    notifyListeners();
  }

  Future<void> signOut() async {
    token = null;
    user = null;
    await _prefs.remove(_tokenKey);
    notifyListeners();
  }

  /// URL for media the player/image widgets load themselves (they can't add headers everywhere).
  String mediaUrl(String path) =>
      '$serverUrl$path${path.contains('?') ? '&' : '?'}access_token=${Uri.encodeQueryComponent(token ?? '')}';

  String videoStreamUrl(String videoId) => mediaUrl('/api/videos/$videoId/stream');
  String thumbnailUrl(String videoId) => mediaUrl('/api/videos/$videoId/thumbnail');
}
