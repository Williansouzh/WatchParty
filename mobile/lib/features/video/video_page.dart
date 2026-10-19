import 'dart:async';

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

class VideoPage extends StatefulWidget {
  const VideoPage({super.key, required this.videoId});

  final String videoId;

  @override
  State<VideoPage> createState() => _VideoPageState();
}

class _VideoPageState extends State<VideoPage> {
  Video? _video;
  Object? _error;
  Timer? _poll;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_video == null && _error == null) _load();
  }

  @override
  void dispose() {
    _poll?.cancel();
    super.dispose();
  }

  Future<void> _load() async {
    try {
      final video = await context.api.video(widget.videoId);
      if (!mounted) return;
      final becameReady = _video?.isProcessing == true && video.isReady;
      setState(() => (_video = video, _error = null));
      if (becameReady) showToast(context, 'Vídeo pronto para assistir!');

      // Keep checking while the server converts the file.
      _poll?.cancel();
      if (video.isProcessing) _poll = Timer(const Duration(seconds: 4), _load);
    } catch (e) {
      if (mounted) setState(() => _error = e);
    }
  }

  Future<void> _toggleFavorite(Video v) async {
    try {
      final isFavorite = await context.api.toggleFavorite(v.id);
      notifyLibraryChanged();
      if (!mounted) return;
      setState(() => _video = v.copyWith(isFavorite: isFavorite));
      showToast(context, isFavorite ? 'Adicionado aos curtidos' : 'Removido dos curtidos');
    } on ApiException catch (e) {
      if (mounted) showToast(context, e.message, error: true);
    }
  }

  Future<void> _edit(Video v) async {
    final values = await showFormDialog(context, title: 'Editar detalhes', fields: [
      FormFieldSpec('title', 'Título', initial: v.title, required: true),
      FormFieldSpec('description', 'Descrição', initial: v.description, multiline: true, maxLength: 2000),
    ]);
    if (values == null || !mounted) return;
    try {
      final description = values['description']!.isEmpty ? null : values['description'];
      await context.api.updateVideo(v.id, title: values['title']!, description: description);
      notifyLibraryChanged();
      if (mounted) setState(() => _video = v.copyWith(title: values['title'], description: description));
    } on ApiException catch (e) {
      if (mounted) showToast(context, e.message, error: true);
    }
  }

  Future<void> _delete(Video v) async {
    if (!await confirmDialog(context, 'Excluir vídeo?', '"${v.title}" será apagado para todo mundo.')) return;
    if (!mounted) return;
    try {
      await context.api.deleteVideo(v.id);
      notifyLibraryChanged();
      if (mounted) {
        showToast(context, 'Vídeo excluído');
        context.pop();
      }
    } on ApiException catch (e) {
      if (mounted) showToast(context, e.message, error: true);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_error != null) return Scaffold(appBar: AppBar(), body: ErrorRetry(error: _error!, onRetry: _load));
    final v = _video;
    if (v == null) return Scaffold(appBar: AppBar(), body: const Center(child: CircularProgressIndicator()));
    final isOwner = v.uploadedById == context.session.user!.id;

    return CollectionView(
      color: colorFor(v.id),
      wideCover: true,
      cover: VideoThumb.of(v, radius: 6),
      kind: 'Vídeo',
      title: v.title,
      meta: [
        v.uploadedByName,
        timeAgo(v.uploadedAt),
        formatDuration(v.durationSeconds),
        formatBytes(v.sizeBytes),
      ].where((s) => s.isNotEmpty).join(' • '),
      onRefresh: _load,
      actions: [
        IconButton(
          key: const Key('favorite'),
          tooltip: v.isFavorite ? 'Remover dos curtidos' : 'Curtir',
          onPressed: () => _toggleFavorite(v),
          icon: Icon(v.isFavorite ? Icons.favorite : Icons.favorite_border, size: 30, color: v.isFavorite ? AppColors.accent : null),
        ),
        IconButton(
          tooltip: 'Adicionar à playlist',
          onPressed: () => addToPlaylistSheet(context, v.id),
          icon: const Icon(Icons.playlist_add, size: 30),
        ),
        if (isOwner)
          PopupMenuButton<String>(
            icon: const Icon(Icons.more_horiz, size: 30),
            color: const Color(0xFF282828),
            onSelected: (choice) => choice == 'edit' ? _edit(v) : _delete(v),
            itemBuilder: (_) => const [
              PopupMenuItem(value: 'edit', child: Row(children: [Icon(Icons.edit), SizedBox(width: 12), Text('Editar detalhes')])),
              PopupMenuItem(value: 'delete', child: Row(children: [Icon(Icons.delete_outline), SizedBox(width: 12), Text('Excluir vídeo')])),
            ],
          ),
        const Spacer(),
        if (v.isReady) PlayFab(key: const Key('watch'), onPressed: () => startRoomForVideo(context, v)),
      ],
      children: [
        if (v.isProcessing)
          const _Banner(
            loading: true,
            title: 'Preparando o vídeo…',
            message: 'Estamos convertendo para funcionar em qualquer celular. Filmes grandes podem levar alguns minutos.',
          ),
        if (v.isFailed)
          _Banner(title: 'Não foi possível converter esse vídeo.', message: '${v.failureReason ?? ''} Tente outro arquivo.', error: true),
        if (v.isReady)
          const Padding(
            padding: EdgeInsets.fromLTRB(16, 8, 16, 0),
            child: Text('Toque no play para criar uma sala e mandar o convite pra galera.', style: TextStyle(color: AppColors.textSub)),
          ),
        if (v.description != null) ...[
          const SectionHeader('Sobre'),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            child: Text(v.description!, style: const TextStyle(color: AppColors.textSub, height: 1.5)),
          ),
        ],
      ],
    );
  }
}

class _Banner extends StatelessWidget {
  const _Banner({required this.title, required this.message, this.loading = false, this.error = false});

  final String title;
  final String message;
  final bool loading;
  final bool error;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 8, 16, 0),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(color: error ? const Color(0xFF4A1A1F) : AppColors.elevated, borderRadius: BorderRadius.circular(8)),
      child: Row(
        children: [
          if (loading) ...[
            const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 3)),
            const SizedBox(width: 16),
          ],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
                const SizedBox(height: 4),
                Text(message, style: const TextStyle(color: AppColors.textSub)),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
