import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../core/api.dart';
import '../../core/app_scope.dart';
import '../../core/format.dart';
import '../../core/models.dart';
import '../../core/theme.dart';
import '../../widgets/actions.dart';
import '../../widgets/collection_view.dart';
import '../../widgets/common.dart';

class LikedPage extends StatefulWidget {
  const LikedPage({super.key});

  @override
  State<LikedPage> createState() => _LikedPageState();
}

class _LikedPageState extends State<LikedPage> {
  List<Video>? _videos;
  Object? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_videos == null && _error == null) _load();
  }

  Future<void> _load() async {
    try {
      final videos = await context.api.videos(filter: 'favorites');
      if (mounted) setState(() => (_videos = videos, _error = null));
    } catch (e) {
      if (mounted) setState(() => _error = e);
    }
  }

  Future<void> _unlike(Video video) async {
    try {
      await context.api.toggleFavorite(video.id);
      notifyLibraryChanged();
      setState(() => _videos!.remove(video));
      if (mounted) showToast(context, 'Removido dos curtidos');
    } on ApiException catch (e) {
      if (mounted) showToast(context, e.message, error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_error != null) return Scaffold(appBar: AppBar(), body: ErrorRetry(error: _error!, onRetry: _load));
    final videos = _videos;
    if (videos == null) return Scaffold(appBar: AppBar(), body: const Center(child: CircularProgressIndicator()));
    final firstReady = videos.where((v) => v.isReady).firstOrNull;

    return CollectionView(
      color: const Color(0xFF5038A0),
      cover: const AspectRatio(aspectRatio: 1, child: FittedBox(child: LikedCover(size: 240))),
      kind: 'Playlist',
      title: 'Vídeos curtidos',
      meta: '${context.session.user!.displayName} • ${plural(videos.length, 'vídeo', 'vídeos')}',
      onRefresh: _load,
      actions: [
        const Spacer(),
        if (firstReady != null) PlayFab(onPressed: () => startRoomForVideo(context, firstReady)),
      ],
      children: videos.isEmpty
          ? const [EmptyState(title: 'Os vídeos que você curtir aparecem aqui', message: 'Salve vídeos tocando no coração.')]
          : [
              for (final v in videos)
                ListRow(
                  leading: VideoThumb.of(v, width: 100, radius: 4),
                  title: v.title,
                  subtitle: videoSubtitle(v),
                  onTap: () => context.push('/video/${v.id}'),
                  trailing: IconButton(
                    tooltip: 'Descurtir',
                    icon: const Icon(Icons.favorite, color: AppColors.accent),
                    onPressed: () => _unlike(v),
                  ),
                ),
            ],
    );
  }
}
