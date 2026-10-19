import 'dart:async';

import 'package:file_picker/file_picker.dart';
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
      if (video.isProcessing) _poll = Timer(const Duration(milliseconds: 2500), _load);
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

  Future<void> _uploadSubtitle(Video v) async {
    final files = await FilePicker.pickFiles(type: FileType.custom, allowedExtensions: ['srt', 'vtt']);
    if (files.isEmpty || !mounted) return;
    final file = files.first;
    final values = await showFormDialog(context, title: 'Nome da legenda', submitLabel: 'Enviar', fields: [
      FormFieldSpec('label', 'Nome (ex: Português)', initial: file.name.replaceFirst(RegExp(r'\.[^.]+$'), ''), maxLength: 60, required: true),
      const FormFieldSpec('language', 'Idioma (ex: pt, en)', maxLength: 16),
    ]);
    if (values == null || !mounted) return;
    try {
      await context.api.uploadSubtitle(v.id, file: file, label: values['label']!, language: values['language']);
      if (mounted) showToast(context, 'Legenda adicionada');
      await _load();
    } on ApiException catch (e) {
      if (mounted) showToast(context, e.message, error: true);
    }
  }

  Future<void> _deleteSubtitle(Video v, SubtitleInfo sub) async {
    try {
      await context.api.deleteSubtitle(v.id, sub.id);
      await _load();
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
        context.canPop() ? context.pop() : context.go('/library');
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
          _Banner(
            loading: true,
            title: 'Preparando o vídeo…${v.processingProgress == null ? '' : ' ${(v.processingProgress! * 100).round()}%'}',
            message: 'Estamos convertendo para funcionar em qualquer celular. Filmes grandes podem levar alguns minutos.',
            progress: v.processingProgress,
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
        if (v.isReady) ...[
          const SectionHeader('Legendas'),
          if (v.subtitles.isEmpty)
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 16),
              child: Text('Nenhuma legenda ainda. As que vêm dentro do arquivo (MKV) aparecem aqui sozinhas.',
                  style: TextStyle(color: AppColors.textSub)),
            ),
          for (final sub in v.subtitles)
            ListTile(
              leading: const Icon(Icons.closed_caption_outlined),
              title: Text(sub.label),
              subtitle: sub.language == null ? null : Text(sub.language!),
              trailing: sub.uploadedById == context.session.user!.id || isOwner
                  ? IconButton(
                      tooltip: 'Apagar legenda',
                      icon: const Icon(Icons.delete_outline),
                      onPressed: () => _deleteSubtitle(v, sub),
                    )
                  : null,
            ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 8, 16, 0),
            child: OutlinedButton.icon(
              onPressed: () => _uploadSubtitle(v),
              icon: const Icon(Icons.upload_file),
              label: const Text('Enviar legenda (.srt ou .vtt)'),
            ),
          ),
          if (v.hasHls)
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 16, 16, 0),
              child: Text('✓ Disponível em várias qualidades: o player se ajusta à internet de cada um.',
                  style: TextStyle(color: AppColors.textSub, fontSize: 13)),
            ),
        ],
      ],
    );
  }
}

class _Banner extends StatelessWidget {
  const _Banner({required this.title, required this.message, this.loading = false, this.error = false, this.progress});

  final String title;
  final String message;
  final bool loading;
  final bool error;
  final double? progress;

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
                if (progress != null) ...[
                  const SizedBox(height: 10),
                  ClipRRect(
                    borderRadius: BorderRadius.circular(3),
                    child: LinearProgressIndicator(value: progress, minHeight: 6, backgroundColor: const Color(0xFF4D4D4D)),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}
