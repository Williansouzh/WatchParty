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

class PlaylistPage extends StatefulWidget {
  const PlaylistPage({super.key, required this.playlistId});

  final String playlistId;

  @override
  State<PlaylistPage> createState() => _PlaylistPageState();
}

class _PlaylistPageState extends State<PlaylistPage> {
  Playlist? _playlist;
  Object? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_playlist == null && _error == null) _load();
  }

  Future<void> _load() async {
    try {
      final playlist = await context.api.playlist(widget.playlistId);
      if (mounted) setState(() => (_playlist = playlist, _error = null));
    } catch (e) {
      if (mounted) setState(() => _error = e);
    }
  }

  Future<void> _run(Future<void> Function() action, {String? success}) async {
    try {
      await action();
      notifyLibraryChanged();
      if (success != null && mounted) showToast(context, success);
      await _load();
    } on ApiException catch (e) {
      if (mounted) showToast(context, e.message, error: true);
    }
  }

  Future<void> _addVideo() async {
    final video = await pickVideo(context, title: 'Adicionar à playlist');
    if (video == null || !mounted) return;
    final api = context.api;
    await _run(() => api.addToPlaylist(widget.playlistId, video.id), success: 'Adicionado');
  }

  Future<void> _edit(Playlist p) async {
    final values = await showFormDialog(context, title: 'Editar playlist', fields: [
      FormFieldSpec('name', 'Nome', initial: p.name, maxLength: 100, required: true),
      FormFieldSpec('description', 'Descrição', initial: p.description, multiline: true, maxLength: 500),
    ]);
    if (values == null || !mounted) return;
    final api = context.api;
    await _run(() => api.updatePlaylist(p.id,
        name: values['name']!, description: values['description']!.isEmpty ? null : values['description']));
  }

  Future<void> _delete(Playlist p) async {
    if (!await confirmDialog(context, 'Excluir playlist?', '"${p.name}" será apagada. Os vídeos continuam na biblioteca.')) return;
    if (!mounted) return;
    try {
      await context.api.deletePlaylist(p.id);
      notifyLibraryChanged();
      if (mounted) context.pop();
    } on ApiException catch (e) {
      if (mounted) showToast(context, e.message, error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_error != null) return Scaffold(appBar: AppBar(), body: ErrorRetry(error: _error!, onRetry: _load));
    final p = _playlist;
    if (p == null) return Scaffold(appBar: AppBar(), body: const Center(child: CircularProgressIndicator()));

    final isOwner = p.ownerId == context.session.user!.id;
    final total = p.items.fold<double>(0, (sum, i) => sum + (i.video.durationSeconds ?? 0));
    final cover = p.items.where((i) => i.video.hasThumbnail).firstOrNull?.video.id;
    final firstReady = p.items.map((i) => i.video).where((v) => v.isReady).firstOrNull;

    return CollectionView(
      color: colorFor(p.id),
      cover: AspectRatio(aspectRatio: 1, child: LayoutBuilder(
        builder: (_, c) => PlaylistCover(playlistId: p.id, coverVideoId: cover, size: c.maxWidth),
      )),
      kind: 'Playlist',
      title: p.name,
      description: p.description,
      meta: '${p.ownerName} • ${plural(p.items.length, 'vídeo', 'vídeos')}${total > 0 ? ', ${formatDuration(total)}' : ''}',
      onRefresh: _load,
      actions: [
        IconButton(tooltip: 'Adicionar vídeos', onPressed: _addVideo, icon: const Icon(Icons.add_circle_outline, size: 30)),
        if (isOwner)
          PopupMenuButton<String>(
            icon: const Icon(Icons.more_horiz, size: 30),
            color: const Color(0xFF282828),
            onSelected: (v) => v == 'edit' ? _edit(p) : _delete(p),
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'edit', child: Row(children: [Icon(Icons.edit), SizedBox(width: 12), Text('Editar detalhes')])),
              PopupMenuItem(value: 'delete', child: Row(children: [Icon(Icons.delete_outline), SizedBox(width: 12), Text('Excluir playlist')])),
            ],
          ),
        const Spacer(),
        if (firstReady != null) PlayFab(onPressed: () => startRoomForVideo(context, firstReady)),
      ],
      children: p.items.isEmpty
          ? [
              EmptyState(
                title: 'Vamos encher essa playlist',
                message: 'Qualquer amigo pode adicionar vídeos aqui.',
                action: OutlinedButton.icon(onPressed: _addVideo, icon: const Icon(Icons.add), label: const Text('Adicionar vídeos')),
              ),
            ]
          : [
              for (final item in p.items)
                ListRow(
                  leading: VideoThumb.of(item.video, width: 100, radius: 4),
                  title: item.video.title,
                  subtitle: '${videoSubtitle(item.video)} • ${timeAgo(item.addedAt)}',
                  onTap: () => context.push('/video/${item.video.id}'),
                  trailing: IconButton(
                    tooltip: 'Remover da playlist',
                    icon: const Icon(Icons.remove_circle_outline, color: AppColors.textSub),
                    onPressed: () => _run(() => context.api.removeFromPlaylist(p.id, item.video.id), success: 'Removido'),
                  ),
                ),
            ],
    );
  }
}
