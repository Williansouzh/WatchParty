import 'package:flutter_test/flutter_test.dart';
import 'package:watchparty/core/format.dart';
import 'package:watchparty/core/session.dart';

void main() {
  test('clock format', () {
    expect(formatClock(0), '0:00');
    expect(formatClock(65), '1:05');
    expect(formatClock(3725), '1:02:05');
    expect(formatClock(double.nan), '0:00');
  });

  test('human durations and sizes', () {
    expect(formatDuration(5400), '1 h 30 min');
    expect(formatDuration(20), '1 min');
    expect(formatDuration(null), '');
    expect(formatBytes(1536), '2 KB');
    expect(formatBytes(3.5 * 1024 * 1024 * 1024), '3.5 GB');
  });

  test('colors are stable per id', () {
    expect(colorFor('abc'), colorFor('abc'));
    expect(parseHex('#1DB954').toARGB32(), 0xFF1DB954);
  });

  test('server url normalisation', () {
    expect(Session.normalizeServer(' 192.168.0.10:5080/ '), 'http://192.168.0.10:5080');
    expect(Session.normalizeServer('https://x.trycloudflare.com//'), 'https://x.trycloudflare.com');
  });
}
