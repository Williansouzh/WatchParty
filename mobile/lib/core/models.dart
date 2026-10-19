// DTOs returned by the WatchParty API (camelCase JSON).

typedef Json = Map<String, dynamic>;

Json asJson(Object? value) => Map<String, dynamic>.from(value as Map);

DateTime _date(Object? value) => DateTime.parse(value as String).toLocal();

class AppUser {
  const AppUser({required this.id, required this.userName, required this.displayName, required this.avatarColor});

  factory AppUser.fromJson(Json j) => AppUser(
        id: j['id'] as String,
        userName: j['userName'] as String,
        displayName: j['displayName'] as String,
        avatarColor: j['avatarColor'] as String,
      );

  final String id;
  final String userName;
  final String displayName;
  final String avatarColor;
}

class Video {
  const Video({
    required this.id,
    required this.title,
    required this.description,
    required this.status,
    required this.failureReason,
    required this.durationSeconds,
    required this.sizeBytes,
    required this.hasThumbnail,
    required this.uploadedById,
    required this.uploadedByName,
    required this.uploadedAt,
    required this.isFavorite,
  });

  factory Video.fromJson(Json j) => Video(
        id: j['id'] as String,
        title: j['title'] as String,
        description: j['description'] as String?,
        status: j['status'] as String,
        failureReason: j['failureReason'] as String?,
        durationSeconds: (j['durationSeconds'] as num?)?.toDouble(),
        sizeBytes: (j['sizeBytes'] as num).toInt(),
        hasThumbnail: j['hasThumbnail'] as bool,
        uploadedById: j['uploadedById'] as String,
        uploadedByName: j['uploadedByName'] as String,
        uploadedAt: _date(j['uploadedAt']),
        isFavorite: j['isFavorite'] as bool,
      );

  final String id;
  final String title;
  final String? description;
  final String status; // Processing | Ready | Failed
  final String? failureReason;
  final double? durationSeconds;
  final int sizeBytes;
  final bool hasThumbnail;
  final String uploadedById;
  final String uploadedByName;
  final DateTime uploadedAt;
  final bool isFavorite;

  bool get isReady => status == 'Ready';
  bool get isProcessing => status == 'Processing';
  bool get isFailed => status == 'Failed';

  Video copyWith({String? title, String? description, bool? isFavorite}) => Video(
        id: id,
        title: title ?? this.title,
        description: description ?? this.description,
        status: status,
        failureReason: failureReason,
        durationSeconds: durationSeconds,
        sizeBytes: sizeBytes,
        hasThumbnail: hasThumbnail,
        uploadedById: uploadedById,
        uploadedByName: uploadedByName,
        uploadedAt: uploadedAt,
        isFavorite: isFavorite ?? this.isFavorite,
      );
}

class PlaylistSummary {
  const PlaylistSummary({
    required this.id,
    required this.name,
    required this.description,
    required this.ownerId,
    required this.ownerName,
    required this.itemCount,
    required this.coverVideoId,
  });

  factory PlaylistSummary.fromJson(Json j) => PlaylistSummary(
        id: j['id'] as String,
        name: j['name'] as String,
        description: j['description'] as String?,
        ownerId: j['ownerId'] as String,
        ownerName: j['ownerName'] as String,
        itemCount: (j['itemCount'] as num).toInt(),
        coverVideoId: j['coverVideoId'] as String?,
      );

  final String id;
  final String name;
  final String? description;
  final String ownerId;
  final String ownerName;
  final int itemCount;
  final String? coverVideoId;
}

class PlaylistItem {
  const PlaylistItem({required this.itemId, required this.addedAt, required this.video});

  factory PlaylistItem.fromJson(Json j) =>
      PlaylistItem(itemId: j['itemId'] as String, addedAt: _date(j['addedAt']), video: Video.fromJson(asJson(j['video'])));

  final String itemId;
  final DateTime addedAt;
  final Video video;
}

class Playlist {
  const Playlist({
    required this.id,
    required this.name,
    required this.description,
    required this.ownerId,
    required this.ownerName,
    required this.items,
  });

  factory Playlist.fromJson(Json j) => Playlist(
        id: j['id'] as String,
        name: j['name'] as String,
        description: j['description'] as String?,
        ownerId: j['ownerId'] as String,
        ownerName: j['ownerName'] as String,
        items: (j['items'] as List).map((e) => PlaylistItem.fromJson(asJson(e))).toList(),
      );

  final String id;
  final String name;
  final String? description;
  final String ownerId;
  final String ownerName;
  final List<PlaylistItem> items;
}

class RoomSummary {
  const RoomSummary({
    required this.code,
    required this.name,
    required this.hostName,
    required this.currentVideoId,
    required this.currentVideoTitle,
    required this.hasThumbnail,
    required this.onlineCount,
  });

  factory RoomSummary.fromJson(Json j) => RoomSummary(
        code: j['code'] as String,
        name: j['name'] as String,
        hostName: j['hostName'] as String,
        currentVideoId: j['currentVideoId'] as String?,
        currentVideoTitle: j['currentVideoTitle'] as String?,
        hasThumbnail: j['hasThumbnail'] as bool,
        onlineCount: (j['onlineCount'] as num).toInt(),
      );

  final String code;
  final String name;
  final String hostName;
  final String? currentVideoId;
  final String? currentVideoTitle;
  final bool hasThumbnail;
  final int onlineCount;
}

/// Position at [updatedAtMs] (server clock, Unix ms); keeps advancing from there while [isPlaying].
class PlaybackState {
  const PlaybackState({required this.videoId, required this.isPlaying, required this.positionSeconds, required this.updatedAtMs});

  factory PlaybackState.fromJson(Json j) => PlaybackState(
        videoId: j['videoId'] as String?,
        isPlaying: j['isPlaying'] as bool,
        positionSeconds: (j['positionSeconds'] as num).toDouble(),
        updatedAtMs: (j['updatedAtMs'] as num).toInt(),
      );

  final String? videoId;
  final bool isPlaying;
  final double positionSeconds;
  final int updatedAtMs;
}

class Room {
  const Room({
    required this.code,
    required this.name,
    required this.hostId,
    required this.hostName,
    required this.currentVideo,
    required this.playback,
  });

  factory Room.fromJson(Json j) => Room(
        code: j['code'] as String,
        name: j['name'] as String,
        hostId: j['hostId'] as String,
        hostName: j['hostName'] as String,
        currentVideo: j['currentVideo'] == null ? null : Video.fromJson(asJson(j['currentVideo'])),
        playback: PlaybackState.fromJson(asJson(j['playback'])),
      );

  final String code;
  final String name;
  final String hostId;
  final String hostName;
  final Video? currentVideo;
  final PlaybackState playback;

  Room withVideo(Video? video) =>
      Room(code: code, name: name, hostId: hostId, hostName: hostName, currentVideo: video, playback: playback);
}

class ChatMessage {
  const ChatMessage({
    required this.id,
    required this.userId,
    required this.displayName,
    required this.avatarColor,
    required this.content,
    required this.sentAt,
  });

  factory ChatMessage.fromJson(Json j) => ChatMessage(
        id: j['id'] as String,
        userId: j['userId'] as String,
        displayName: j['displayName'] as String,
        avatarColor: j['avatarColor'] as String,
        content: j['content'] as String,
        sentAt: _date(j['sentAt']),
      );

  final String id;
  final String userId;
  final String displayName;
  final String avatarColor;
  final String content;
  final DateTime sentAt;
}

class Participant {
  const Participant({
    required this.connectionId,
    required this.userId,
    required this.displayName,
    required this.avatarColor,
    required this.inVoice,
    required this.isMuted,
  });

  factory Participant.fromJson(Json j) => Participant(
        connectionId: j['connectionId'] as String,
        userId: j['userId'] as String,
        displayName: j['displayName'] as String,
        avatarColor: j['avatarColor'] as String,
        inVoice: j['inVoice'] as bool,
        isMuted: j['isMuted'] as bool,
      );

  final String connectionId;
  final String userId;
  final String displayName;
  final String avatarColor;
  final bool inVoice;
  final bool isMuted;

  Participant copyWith({bool? inVoice, bool? isMuted}) => Participant(
        connectionId: connectionId,
        userId: userId,
        displayName: displayName,
        avatarColor: avatarColor,
        inVoice: inVoice ?? this.inVoice,
        isMuted: isMuted ?? this.isMuted,
      );
}
