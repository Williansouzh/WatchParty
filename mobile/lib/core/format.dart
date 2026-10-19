import 'dart:ui';

String formatClock(double? totalSeconds) {
  if (totalSeconds == null || !totalSeconds.isFinite || totalSeconds < 0) return '0:00';
  final s = totalSeconds.floor();
  final h = s ~/ 3600, m = (s % 3600) ~/ 60, sec = s % 60;
  final mm = h > 0 ? m.toString().padLeft(2, '0') : '$m';
  return '${h > 0 ? '$h:' : ''}$mm:${sec.toString().padLeft(2, '0')}';
}

String formatDuration(double? totalSeconds) {
  if (totalSeconds == null || totalSeconds <= 0) return '';
  final h = totalSeconds ~/ 3600;
  final m = ((totalSeconds % 3600) / 60).round();
  return h > 0 ? '$h h $m min' : '${m < 1 ? 1 : m} min';
}

String formatBytes(num bytes) {
  if (bytes <= 0) return '';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  var value = bytes.toDouble();
  var i = 0;
  while (value >= 1024 && i < units.length - 1) {
    value /= 1024;
    i++;
  }
  return '${value.toStringAsFixed(i >= 3 ? 1 : 0)} ${units[i]}';
}

String timeAgo(DateTime date) {
  final diff = DateTime.now().difference(date);
  if (diff.inSeconds < 60) return 'agora';
  if (diff.inMinutes < 60) return 'há ${diff.inMinutes} min';
  if (diff.inHours < 24) return 'há ${diff.inHours} h';
  if (diff.inDays < 7) return diff.inDays == 1 ? 'ontem' : 'há ${diff.inDays} dias';
  if (diff.inDays < 30) return 'há ${diff.inDays ~/ 7} sem.';
  if (diff.inDays < 365) return 'há ${diff.inDays ~/ 30} meses';
  return 'há ${diff.inDays ~/ 365} anos';
}

String timeOfDay(DateTime date) =>
    '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';

String greeting() {
  final h = DateTime.now().hour;
  return h < 5 ? 'Boa noite' : h < 12 ? 'Bom dia' : h < 18 ? 'Boa tarde' : 'Boa noite';
}

String plural(int n, String one, String many) => '$n ${n == 1 ? one : many}';

const _palette = [
  0xFFE13300, 0xFF1E3264, 0xFF8D67AB, 0xFFE8115B, 0xFF148A08, 0xFFBC5900,
  0xFF509BF5, 0xFF477D95, 0xFFAF2896, 0xFF503750, 0xFF27856A, 0xFFDC148C,
];

/// Stable color for an id, used for placeholders and page headers.
Color colorFor(String seed) {
  var hash = 0;
  for (final c in seed.codeUnits) {
    hash = (hash * 31 + c) & 0x7fffffff;
  }
  return Color(_palette[hash % _palette.length]);
}

Color parseHex(String hex) {
  final value = int.tryParse(hex.replaceFirst('#', ''), radix: 16) ?? 0x1DB954;
  return Color(0xFF000000 | value);
}
