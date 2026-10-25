import 'package:dio/dio.dart';
import 'package:file_picker/file_picker.dart';

import 'models.dart';
import 'session.dart';

class ApiException implements Exception {
  const ApiException(this.message, [this.statusCode]);

  final String message;
  final int? statusCode;

  @override
  String toString() => message;
}

/// REST client for the WatchParty server. Errors come back as ProblemDetails whose `title` is user-facing.
class Api {
  Api(this.session) {
    _dio.interceptors.add(InterceptorsWrapper(onRequest: (options, handler) {
      options.baseUrl = session.serverUrl ?? '';
      final token = session.token;
      if (token != null) options.headers['Authorization'] = 'Bearer $token';
      handler.next(options);
    }));
  }

  final Session session;
  final _dio = Dio(BaseOptions(
    connectTimeout: const Duration(seconds: 10),
    receiveTimeout: const Duration(seconds: 30),
  ));

  Future<T> _call<T>(Future<Response<dynamic>> Function() request, T Function(dynamic data) parse) async {
    Response<dynamic>? response;
    try {
      response = await request();
      return parse(response.data);
    } on DioException catch (e) {
      throw _toApiException(e);
    } on TypeError catch (e) {
      // A 2xx answer that isn't our JSON (e.g. an HTML page): usually a wrong server address.
      final body = response?.data.toString() ?? '';
      // ignore: avoid_print
      print('WatchParty: unexpected response from ${response?.realUri} (${response?.statusCode}): $e\n'
          '${body.length > 300 ? body.substring(0, 300) : body}');
      throw const ApiException('Resposta inesperada do servidor. Confira o endereço.');
    }
  }

  ApiException _toApiException(DioException e) {
    final status = e.response?.statusCode;
    if (status == 401 && !e.requestOptions.path.startsWith('/api/auth/token')) session.signOut();

    final data = e.response?.data;
    final title = data is Map ? data['title'] as String? : null;
    if (status == 429) return ApiException('Muitas tentativas. Espere um minuto.', status);
    if (title != null) return ApiException(title, status);

    return switch (e.type) {
      DioExceptionType.connectionError ||
      DioExceptionType.connectionTimeout ||
      DioExceptionType.receiveTimeout =>
        const ApiException('Sem conexão com o servidor. Confira o endereço e sua internet.'),
      DioExceptionType.cancel => const ApiException('Cancelado.'),
      _ => ApiException('Algo deu errado.', status),
    };
  }

  List<T> _list<T>(dynamic data, T Function(Json) parse) =>
      (data as List).map((e) => parse(asJson(e))).toList();

  // ---------- Auth ----------

  Future<String> login(String userName, String password) => _call(
        () => _dio.post('/api/auth/token/login', data: {'userName': userName, 'password': password}),
        (d) => asJson(d)['accessToken'] as String,
      );

  Future<String> register({
    required String userName,
    required String displayName,
    required String password,
    required String inviteCode,
  }) =>
      _call(
        () => _dio.post('/api/auth/token/register', data: {
          'userName': userName,
          'displayName': displayName,
          'password': password,
          'inviteCode': inviteCode,
        }),
        (d) => asJson(d)['accessToken'] as String,
      );

  /// Uses [token] explicitly so it works before the session stores it.
  Future<AppUser> me({String? token}) => _call(
        () => _dio.get('/api/auth/me',
            options: token == null ? null : Options(headers: {'Authorization': 'Bearer $token'})),
        (d) => AppUser.fromJson(asJson(d)),
      );

  /// Returns a new token for this device; every other device is signed out.
  Future<String> changePassword(String currentPassword, String newPassword) => _call(
        () => _dio.post('/api/auth/password', data: {'currentPassword': currentPassword, 'newPassword': newPassword}),
        (d) => asJson(d)['accessToken'] as String,
      );

  Future<void> logoutEverywhere() => _call(() => _dio.post('/api/auth/logout-everywhere'), (_) {});

  /// Newest Android build announced by the server.
  Future<({int latestBuild, String? latestVersion, String? downloadUrl})> appInfo() => _call(
        () => _dio.get('/api/app'),
        (d) {
          final j = asJson(d);
          return (
            latestBuild: (j['latestBuild'] as num?)?.toInt() ?? 0,
            latestVersion: j['latestVersion'] as String?,
            downloadUrl: j['downloadUrl'] as String?,
          );
        },
      );

  Future<RtcConfig> rtcConfig() => _call(
        () => _dio.get('/api/config'),
        (d) {
          final j = asJson(d);
          return RtcConfig(
            iceServers: (j['iceServers'] as List).map((e) {
              final server = asJson(e);
              server.removeWhere((_, value) => value == null);
              return server;
            }).toList(),
            liveKitUrl: j['voice'] == 'livekit' ? j['liveKitUrl'] as String? : null,
          );
        },
      );

  // ---------- Videos ----------

  Future<List<Video>> videos({String? search, String? filter, int? take}) => _call(
        () => _dio.get('/api/videos', queryParameters: {
          if (search != null && search.isNotEmpty) 'search': search,
          'filter': ?filter,
          'take': ?take,
        }),
        (d) => _list(d, Video.fromJson),
      );

  Future<Video> video(String id) => _call(() => _dio.get('/api/videos/$id'), (d) => Video.fromJson(asJson(d)));

  Future<void> updateVideo(String id, {required String title, String? description}) =>
      _call(() => _dio.put('/api/videos/$id', data: {'title': title, 'description': description}), (_) {});

  Future<void> deleteVideo(String id) => _call(() => _dio.delete('/api/videos/$id'), (_) {});

  Future<bool> toggleFavorite(String id) =>
      _call(() => _dio.post('/api/videos/$id/favorite'), (d) => asJson(d)['isFavorite'] as bool);

  /// Streams the file from disk; "title" goes before the file because the server reads the body sequentially.
  Future<String> uploadVideo({
    required PlatformFile file,
    required String title,
    void Function(double progress)? onProgress,
    CancelToken? cancelToken,
  }) async {
    final length = await file.length() ?? 0;
    final form = FormData()
      ..fields.add(MapEntry('title', title))
      ..files.add(MapEntry('file', MultipartFile.fromStream(() => file.readAsByteStream(), length, filename: file.name)));

    return _call(
      () => _dio.post(
        '/api/videos',
        data: form,
        cancelToken: cancelToken,
        options: Options(sendTimeout: const Duration(hours: 6), receiveTimeout: const Duration(minutes: 10)),
        onSendProgress: (sent, total) {
          if (total > 0) onProgress?.call(sent / total);
        },
      ),
      (d) => asJson(d)['id'] as String,
    );
  }

  Future<SubtitleInfo> uploadSubtitle(String videoId, {required PlatformFile file, required String label, String? language}) async {
    final bytes = await file.readAsBytes();
    final form = FormData()
      ..fields.add(MapEntry('label', label))
      ..fields.add(MapEntry('language', language ?? ''))
      ..files.add(MapEntry('file', MultipartFile.fromBytes(bytes, filename: file.name)));
    return _call(() => _dio.post('/api/videos/$videoId/subtitles', data: form), (d) => SubtitleInfo.fromJson(asJson(d)));
  }

  Future<void> deleteSubtitle(String videoId, String subtitleId) =>
      _call(() => _dio.delete('/api/videos/$videoId/subtitles/$subtitleId'), (_) {});

  /// WebVTT text of a subtitle track.
  Future<String> subtitleText(String videoId, String subtitleId) => _call(
        () => _dio.get('/api/videos/$videoId/subtitles/$subtitleId', options: Options(responseType: ResponseType.plain)),
        (d) => d as String,
      );

  // ---------- Playlists ----------

  Future<List<PlaylistSummary>> playlists() =>
      _call(() => _dio.get('/api/playlists'), (d) => _list(d, PlaylistSummary.fromJson));

  Future<Playlist> playlist(String id) =>
      _call(() => _dio.get('/api/playlists/$id'), (d) => Playlist.fromJson(asJson(d)));

  Future<String> createPlaylist(String name, {String? description}) => _call(
        () => _dio.post('/api/playlists', data: {'name': name, 'description': description}),
        (d) => asJson(d)['id'] as String,
      );

  Future<void> updatePlaylist(String id, {required String name, String? description}) =>
      _call(() => _dio.put('/api/playlists/$id', data: {'name': name, 'description': description}), (_) {});

  Future<void> deletePlaylist(String id) => _call(() => _dio.delete('/api/playlists/$id'), (_) {});

  Future<void> addToPlaylist(String playlistId, String videoId) =>
      _call(() => _dio.post('/api/playlists/$playlistId/videos', data: {'videoId': videoId}), (_) {});

  Future<void> removeFromPlaylist(String playlistId, String videoId) =>
      _call(() => _dio.delete('/api/playlists/$playlistId/videos/$videoId'), (_) {});

  // ---------- Rooms ----------

  Future<List<RoomSummary>> rooms() => _call(() => _dio.get('/api/rooms'), (d) => _list(d, RoomSummary.fromJson));

  /// With [playlistId] the room starts a "maratona": first video now, the rest queued.
  Future<String> createRoom(String name, {String? videoId, String? playlistId}) => _call(
        () => _dio.post('/api/rooms', data: {'name': name, 'videoId': videoId, 'playlistId': playlistId}),
        (d) => asJson(d)['code'] as String,
      );

  Future<void> deleteRoom(String code) => _call(() => _dio.delete('/api/rooms/$code'), (_) {});
}

/// How voice calls connect: WebRTC mesh with these ICE servers, or a LiveKit server when [liveKitUrl] is set.
class RtcConfig {
  const RtcConfig({this.iceServers = const [], this.liveKitUrl});

  final List<Json> iceServers;
  final String? liveKitUrl;
}
