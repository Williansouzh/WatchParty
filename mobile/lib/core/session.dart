import 'package:flutter/foundation.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'models.dart';

/// Which server we talk to, the access token and the signed-in user. Persisted between launches;
/// the token lives in the Keychain/Keystore (secure storage), not in plain preferences.
class Session extends ChangeNotifier {
  Session._(this._prefs, this._secure);

  static const _serverKey = 'server_url';
  static const _tokenKey = 'access_token';

  /// Can be baked in at build time: flutter build apk --dart-define=SERVER_URL=https://...
  static const _defaultServer = String.fromEnvironment('SERVER_URL');

  final SharedPreferences _prefs;
  final FlutterSecureStorage _secure;

  String? serverUrl;
  String? token;
  AppUser? user;

  bool get isSignedIn => user != null && token != null;

  static Future<Session> load() async {
    final prefs = await SharedPreferences.getInstance();
    const secure = FlutterSecureStorage();
    final session = Session._(prefs, secure);
    session.serverUrl = prefs.getString(_serverKey) ??
        (_defaultServer.isNotEmpty ? _defaultServer : (kIsWeb ? Uri.base.origin : null));

    session.token = await _readSecure(secure);
    // Versions before 0.2 kept the token in plain preferences: move it.
    final legacy = prefs.getString(_tokenKey);
    if (legacy != null) {
      session.token ??= legacy;
      await _writeSecure(secure, session.token);
      await prefs.remove(_tokenKey);
    }
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
    await _writeSecure(_secure, accessToken);
    notifyListeners();
  }

  /// A new token for the same user (e.g. after changing the password).
  Future<void> replaceToken(String accessToken) async {
    token = accessToken;
    await _writeSecure(_secure, accessToken);
  }

  void restore(AppUser signedInUser) {
    user = signedInUser;
    notifyListeners();
  }

  Future<void> signOut() async {
    token = null;
    user = null;
    await _writeSecure(_secure, null);
    notifyListeners();
  }

  /// URL for media the player/image widgets load themselves (they can't add headers everywhere).
  String mediaUrl(String path) =>
      '$serverUrl$path${path.contains('?') ? '&' : '?'}access_token=${Uri.encodeQueryComponent(token ?? '')}';

  String videoStreamUrl(String videoId) => mediaUrl('/api/videos/$videoId/stream');
  String hlsUrl(String videoId) => '$serverUrl/api/videos/$videoId/hls/master.m3u8';
  String thumbnailUrl(String videoId) => mediaUrl('/api/videos/$videoId/thumbnail');
  Map<String, String> get authHeaders => {if (token != null) 'Authorization': 'Bearer $token'};

  static Future<String?> _readSecure(FlutterSecureStorage storage) async {
    try {
      return await storage.read(key: _tokenKey);
    } catch (_) {
      return null; // e.g. keystore reset after a backup restore: just log in again
    }
  }

  static Future<void> _writeSecure(FlutterSecureStorage storage, String? value) async {
    try {
      value == null ? await storage.delete(key: _tokenKey) : await storage.write(key: _tokenKey, value: value);
    } catch (_) {/* keep the in-memory session working */}
  }
}
