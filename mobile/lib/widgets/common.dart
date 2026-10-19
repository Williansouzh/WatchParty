import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../core/api.dart';
import '../core/app_scope.dart';
import '../core/format.dart';
import '../core/models.dart';
import '../core/theme.dart';

/// [bottomOffset] lifts the toast so it doesn't cover controls at the bottom of the screen.
void showToast(BuildContext context, String message, {bool error = false, double bottomOffset = 0}) {
  final messenger = ScaffoldMessenger.maybeOf(context);
  messenger?.hideCurrentSnackBar();
  messenger?.showSnackBar(SnackBar(
    content: Text(message, style: TextStyle(color: error ? Colors.white : Colors.black, fontWeight: FontWeight.w600)),
    backgroundColor: error ? const Color(0xFFE9142A) : AppColors.text,
    duration: const Duration(seconds: 3),
    margin: bottomOffset > 0 ? EdgeInsets.fromLTRB(16, 0, 16, bottomOffset) : null,
  ));
}

class Avatar extends StatelessWidget {
  const Avatar({super.key, required this.name, required this.color, this.size = 32, this.speaking = false, this.dimmed = false});

  final String name;
  final String color;
  final double size;
  final bool speaking;
  final bool dimmed;

  @override
  Widget build(BuildContext context) {
    final letter = name.trim().isEmpty ? '?' : name.trim()[0].toUpperCase();
    return AnimatedContainer(
      duration: const Duration(milliseconds: 150),
      padding: EdgeInsets.all(speaking ? 2 : 0),
      decoration: BoxDecoration(
        shape: BoxShape.circle,
        border: speaking ? Border.all(color: AppColors.accent, width: 2) : null,
      ),
      child: Opacity(
        opacity: dimmed ? 0.5 : 1,
        child: CircleAvatar(
          radius: size / 2,
          backgroundColor: parseHex(color),
          child: Text(letter,
              style: TextStyle(color: Colors.black, fontWeight: FontWeight.w800, fontSize: size * 0.42)),
        ),
      ),
    );
  }
}

/// 16:9 thumbnail, or a colored placeholder while there is none.
class VideoThumb extends StatelessWidget {
  const VideoThumb({
    super.key,
    required this.videoId,
    required this.hasThumbnail,
    this.title = '',
    this.width,
    this.radius = 6,
    this.icon = Icons.movie_rounded,
  });

  VideoThumb.of(Video video, {Key? key, double? width, double radius = 6})
      : this(key: key, videoId: video.id, hasThumbnail: video.hasThumbnail, title: video.title, width: width, radius: radius);

  final String videoId;
  final bool hasThumbnail;
  final String title;
  final double? width;
  final double radius;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    final placeholder = GradientPlaceholder(seed: videoId, seed2: '${title}z', icon: icon);
    return SizedBox(
      width: width,
      child: AspectRatio(
        aspectRatio: 16 / 9,
        child: ClipRRect(
          borderRadius: BorderRadius.circular(radius),
          child: hasThumbnail
              ? Image.network(
                  context.session.thumbnailUrl(videoId),
                  fit: BoxFit.cover,
                  errorBuilder: (_, _, _) => placeholder,
                  loadingBuilder: (_, child, progress) => progress == null ? child : placeholder,
                )
              : placeholder,
        ),
      ),
    );
  }
}

class GradientPlaceholder extends StatelessWidget {
  const GradientPlaceholder({super.key, required this.seed, this.seed2, this.icon = Icons.movie_rounded});

  final String seed;
  final String? seed2;
  final IconData icon;

  @override
  Widget build(BuildContext context) {
    return DecoratedBox(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [colorFor(seed), seed2 == null ? AppColors.background : colorFor(seed2!)],
        ),
      ),
      child: LayoutBuilder(
        builder: (_, c) => Center(child: Icon(icon, color: Colors.white70, size: c.biggest.shortestSide * 0.4)),
      ),
    );
  }
}

class LikedCover extends StatelessWidget {
  const LikedCover({super.key, this.size = 48});

  final double size;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(4),
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [AppColors.liked1, AppColors.liked2],
        ),
      ),
      child: Icon(Icons.favorite, color: Colors.white, size: size * 0.45),
    );
  }
}

class PlaylistCover extends StatelessWidget {
  const PlaylistCover({super.key, required this.playlistId, this.coverVideoId, this.size = 48});

  final String playlistId;
  final String? coverVideoId;
  final double size;

  @override
  Widget build(BuildContext context) {
    final placeholder = GradientPlaceholder(seed: playlistId, icon: Icons.queue_music_rounded);
    return ClipRRect(
      borderRadius: BorderRadius.circular(4),
      child: SizedBox(
        width: size,
        height: size,
        child: coverVideoId == null
            ? placeholder
            : Image.network(context.session.thumbnailUrl(coverVideoId!),
                fit: BoxFit.cover, errorBuilder: (_, _, _) => placeholder),
      ),
    );
  }
}

class SectionHeader extends StatelessWidget {
  const SectionHeader(this.title, {super.key, this.onMore});

  final String title;
  final VoidCallback? onMore;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 24, 8, 12),
      child: Row(
        children: [
          Expanded(child: Text(title, style: Theme.of(context).textTheme.titleLarge)),
          if (onMore != null)
            TextButton(
              onPressed: onMore,
              child: const Text('Mostrar tudo',
                  style: TextStyle(color: AppColors.textSub, fontWeight: FontWeight.w700, fontSize: 13)),
            ),
        ],
      ),
    );
  }
}

class EmptyState extends StatelessWidget {
  const EmptyState({super.key, required this.title, this.message, this.action});

  final String title;
  final String? message;
  final Widget? action;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 32, vertical: 48),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(title, textAlign: TextAlign.center, style: Theme.of(context).textTheme.titleLarge),
          if (message != null) ...[
            const SizedBox(height: 8),
            Text(message!, textAlign: TextAlign.center, style: const TextStyle(color: AppColors.textSub)),
          ],
          if (action != null) ...[const SizedBox(height: 20), action!],
        ],
      ),
    );
  }
}

class ErrorRetry extends StatelessWidget {
  const ErrorRetry({super.key, required this.error, required this.onRetry});

  final Object error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return EmptyState(
      title: 'Ops!',
      message: error is ApiException ? error.toString() : 'Algo deu errado.',
      action: OutlinedButton(onPressed: onRetry, child: const Text('Tentar de novo')),
    );
  }
}

/// Green round play button, Spotify style.
class PlayFab extends StatelessWidget {
  const PlayFab({
    super.key,
    required this.onPressed,
    this.size = 56,
    this.icon = Icons.play_arrow_rounded,
    this.label = 'Assistir em grupo',
  });

  final VoidCallback? onPressed;
  final double size;
  final IconData icon;
  final String label;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: label,
      excludeSemantics: true,
      child: Material(
      color: AppColors.accent,
      shape: const CircleBorder(),
      elevation: 4,
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onPressed,
          child: SizedBox(width: size, height: size, child: Icon(icon, color: Colors.black, size: size * 0.55)),
        ),
      ),
    );
  }
}

String videoSubtitle(Video v) {
  if (v.isProcessing) return 'Processando…';
  if (v.isFailed) return 'Falhou ao converter';
  return [formatDuration(v.durationSeconds), v.uploadedByName].where((s) => s.isNotEmpty).join(' • ');
}

/// Card used in carousels and grids.
class VideoCard extends StatelessWidget {
  const VideoCard({super.key, required this.video, this.width});

  final Video video;
  final double? width;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: width,
      child: InkWell(
        borderRadius: BorderRadius.circular(6),
        onTap: () => context.push('/video/${video.id}'),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Stack(
              children: [
                VideoThumb.of(video),
                if (!video.isReady)
                  Positioned(
                    left: 6,
                    top: 6,
                    child: _Badge(video.isProcessing ? 'PROCESSANDO' : 'FALHOU',
                        color: video.isFailed ? AppColors.danger : Colors.black87),
                  ),
                if (video.durationSeconds != null)
                  Positioned(right: 6, bottom: 6, child: _Badge(formatClock(video.durationSeconds), color: Colors.black87)),
              ],
            ),
            const SizedBox(height: 8),
            Text(video.title, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 2),
            Text(videoSubtitle(video), maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}

class RoomCard extends StatelessWidget {
  const RoomCard({super.key, required this.room, this.width});

  final RoomSummary room;
  final double? width;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: width,
      child: InkWell(
        borderRadius: BorderRadius.circular(6),
        onTap: () => context.push('/room/${room.code}'),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Stack(
              children: [
                room.currentVideoId == null
                    ? AspectRatio(
                        aspectRatio: 16 / 9,
                        child: ClipRRect(
                          borderRadius: BorderRadius.circular(6),
                          child: GradientPlaceholder(seed: room.code, icon: Icons.live_tv_rounded),
                        ),
                      )
                    : VideoThumb(
                        videoId: room.currentVideoId!,
                        hasThumbnail: room.hasThumbnail,
                        title: room.currentVideoTitle ?? '',
                      ),
                if (room.onlineCount > 0)
                  Positioned(left: 6, top: 6, child: _Badge('AO VIVO • ${room.onlineCount}', color: AppColors.accent, dark: true)),
              ],
            ),
            const SizedBox(height: 8),
            Text(room.name, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w700)),
            const SizedBox(height: 2),
            Text('${room.currentVideoTitle ?? 'Sem vídeo'} • ${room.hostName}',
                maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.bodySmall),
          ],
        ),
      ),
    );
  }
}

class _Badge extends StatelessWidget {
  const _Badge(this.text, {required this.color, this.dark = false});

  final String text;
  final Color color;
  final bool dark;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(color: color, borderRadius: BorderRadius.circular(4)),
      child: Text(text,
          style: TextStyle(
              fontSize: 10, fontWeight: FontWeight.w800, letterSpacing: 0.5, color: dark ? Colors.black : Colors.white)),
    );
  }
}

/// A horizontally scrolling row of cards (Spotify's home carousels).
class Carousel extends StatelessWidget {
  const Carousel({super.key, required this.children, this.height = 190});

  final List<Widget> children;
  final double height;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: height,
      child: ListView.separated(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 16),
        itemCount: children.length,
        separatorBuilder: (_, _) => const SizedBox(width: 14),
        itemBuilder: (_, i) => children[i],
      ),
    );
  }
}

/// Spotify-style list row with a square or 16:9 leading image.
class ListRow extends StatelessWidget {
  const ListRow({super.key, required this.leading, required this.title, this.subtitle, this.trailing, this.onTap, this.highlight = false});

  final Widget leading;
  final String title;
  final String? subtitle;
  final Widget? trailing;
  final VoidCallback? onTap;
  final bool highlight;

  @override
  Widget build(BuildContext context) {
    return InkWell(
      onTap: onTap,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 6),
        child: Row(
          children: [
            leading,
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(fontWeight: FontWeight.w600, fontSize: 15, color: highlight ? AppColors.accent : AppColors.text)),
                  if (subtitle != null) ...[
                    const SizedBox(height: 2),
                    Text(subtitle!, maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.bodySmall),
                  ],
                ],
              ),
            ),
            ?trailing,
          ],
        ),
      ),
    );
  }
}
