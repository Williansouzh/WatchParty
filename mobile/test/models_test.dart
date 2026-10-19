import 'package:flutter_test/flutter_test.dart';
import 'package:watchparty/core/models.dart';

void main() {
  test('parses a room as returned by the hub', () {
    final room = Room.fromJson({
      'id': 'r1',
      'code': 'K7P2QX',
      'name': 'Sexta',
      'hostId': 'u1',
      'hostName': 'Will',
      'onlineCount': 2,
      'createdAt': '2026-01-01T20:00:00Z',
      'currentVideo': {
        'id': 'v1',
        'title': 'Filme',
        'description': null,
        'status': 'Ready',
        'failureReason': null,
        'durationSeconds': 5400,
        'sizeBytes': 123,
        'hasThumbnail': true,
        'uploadedById': 'u1',
        'uploadedByName': 'Will',
        'uploadedAt': '2026-01-01T19:00:00Z',
        'isFavorite': false,
      },
      'playback': {'videoId': 'v1', 'isPlaying': true, 'positionSeconds': 12.5, 'updatedAtMs': 1767297600000},
    });

    expect(room.currentVideo!.isReady, isTrue);
    expect(room.currentVideo!.durationSeconds, 5400);
    expect(room.playback.isPlaying, isTrue);
    expect(room.playback.positionSeconds, 12.5);
    expect(room.withVideo(null).currentVideo, isNull);
  });

  test('participant copyWith keeps identity', () {
    const p = Participant(connectionId: 'c', userId: 'u', displayName: 'Ana', avatarColor: '#fff', inVoice: false, isMuted: false);
    final q = p.copyWith(inVoice: true);
    expect(q.inVoice, isTrue);
    expect(q.isMuted, isFalse);
    expect(q.connectionId, 'c');
  });
}
