import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../core/api.dart';
import '../core/app_scope.dart';
import '../core/models.dart';
import '../core/theme.dart';
import 'common.dart';

/// Bumped whenever playlists/likes/uploads change so open lists can refresh.
final libraryVersion = ValueNotifier<int>(0);
void notifyLibraryChanged() => libraryVersion.value++;

class FormFieldSpec {
  const FormFieldSpec(this.name, this.label, {this.initial, this.multiline = false, this.maxLength = 200, this.required = false});

  final String name;
  final String label;
  final String? initial;
  final bool multiline;
  final int maxLength;
  final bool required;
}

Future<Map<String, String>?> showFormDialog(
  BuildContext context, {
  required String title,
  required List<FormFieldSpec> fields,
  String submitLabel = 'Salvar',
}) {
  final controllers = {for (final f in fields) f.name: TextEditingController(text: f.initial ?? '')};
  final formKey = GlobalKey<FormState>();

  return showDialog<Map<String, String>>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(title, style: const TextStyle(fontWeight: FontWeight.w800)),
      content: Form(
        key: formKey,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              for (final f in fields)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: TextFormField(
                    controller: controllers[f.name],
                    autofocus: f == fields.first,
                    maxLength: f.maxLength,
                    minLines: f.multiline ? 3 : 1,
                    maxLines: f.multiline ? 5 : 1,
                    decoration: InputDecoration(labelText: f.label, counterText: ''),
                    validator: (v) => f.required && (v == null || v.trim().isEmpty) ? 'Obrigatório' : null,
                  ),
                ),
            ],
          ),
        ),
      ),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context), child: const Text('Cancelar', style: TextStyle(color: AppColors.textSub))),
        FilledButton(
          onPressed: () {
            if (!formKey.currentState!.validate()) return;
            Navigator.pop(context, {for (final e in controllers.entries) e.key: e.value.text.trim()});
          },
          child: Text(submitLabel),
        ),
      ],
    ),
  );
}

Future<bool> confirmDialog(BuildContext context, String title, String message, {String confirmLabel = 'Excluir'}) async {
  final result = await showDialog<bool>(
    context: context,
    builder: (context) => AlertDialog(
      title: Text(title, style: const TextStyle(fontWeight: FontWeight.w800)),
      content: Text(message, style: const TextStyle(color: AppColors.textSub)),
      actions: [
        TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancelar', style: TextStyle(color: AppColors.textSub))),
        FilledButton(onPressed: () => Navigator.pop(context, true), child: Text(confirmLabel)),
      ],
    ),
  );
  return result ?? false;
}

/// Bottom sheet that lets the user pick a ready video from the library.
Future<Video?> pickVideo(BuildContext context, {String title = 'Escolher vídeo'}) {
  return showModalBottomSheet<Video>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    builder: (_) => _VideoPickerSheet(title: title),
  );
}

class _VideoPickerSheet extends StatefulWidget {
  const _VideoPickerSheet({required this.title});

  final String title;

  @override
  State<_VideoPickerSheet> createState() => _VideoPickerSheetState();
}

class _VideoPickerSheetState extends State<_VideoPickerSheet> {
  List<Video>? _videos;
  String? _error;
  Timer? _debounce;

  @override
  void initState() {
    super.initState();
    _load('');
  }

  @override
  void dispose() {
    _debounce?.cancel();
    super.dispose();
  }

  Future<void> _load(String search) async {
    try {
      final videos = await context.api.videos(search: search);
      if (mounted) setState(() => _videos = videos.where((v) => v.isReady).toList());
    } on ApiException catch (e) {
      if (mounted) setState(() => _error = e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    return DraggableScrollableSheet(
      expand: false,
      initialChildSize: 0.75,
      maxChildSize: 0.95,
      builder: (context, scroll) => Column(
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(widget.title, style: Theme.of(context).textTheme.titleLarge),
                const SizedBox(height: 12),
                TextField(
                  decoration: const InputDecoration(hintText: 'Buscar na biblioteca', prefixIcon: Icon(Icons.search)),
                  onChanged: (v) {
                    _debounce?.cancel();
                    _debounce = Timer(const Duration(milliseconds: 250), () => _load(v.trim()));
                  },
                ),
              ],
            ),
          ),
          Expanded(
            child: _error != null
                ? Center(child: Text(_error!))
                : _videos == null
                    ? const Center(child: CircularProgressIndicator())
                    : _videos!.isEmpty
                        ? const EmptyState(title: 'Nenhum vídeo pronto', message: 'Envie um vídeo ou espere a conversão terminar.')
                        : ListView.builder(
                            controller: scroll,
                            itemCount: _videos!.length,
                            itemBuilder: (_, i) {
                              final v = _videos![i];
                              return ListRow(
                                leading: VideoThumb.of(v, width: 96, radius: 4),
                                title: v.title,
                                subtitle: videoSubtitle(v),
                                onTap: () => Navigator.pop(context, v),
                              );
                            },
                          ),
          ),
        ],
      ),
    );
  }
}

Future<void> startRoomForVideo(BuildContext context, Video video) async {
  try {
    final name = 'Sessão: ${video.title}';
    final code = await context.api.createRoom(name.length > 80 ? name.substring(0, 80) : name, videoId: video.id);
    if (context.mounted) context.push('/room/$code');
  } on ApiException catch (e) {
    if (context.mounted) showToast(context, e.message, error: true);
  }
}

Future<String?> createPlaylistFlow(BuildContext context, {bool open = true}) async {
  final values = await showFormDialog(
    context,
    title: 'Nova playlist',
    submitLabel: 'Criar',
    fields: const [
      FormFieldSpec('name', 'Nome', maxLength: 100, required: true),
      FormFieldSpec('description', 'Descrição (opcional)', multiline: true, maxLength: 500),
    ],
  );
  if (values == null || !context.mounted) return null;
  try {
    final id = await context.api.createPlaylist(values['name']!,
        description: values['description']!.isEmpty ? null : values['description']);
    notifyLibraryChanged();
    if (open && context.mounted) context.push('/playlist/$id');
    return id;
  } on ApiException catch (e) {
    if (context.mounted) showToast(context, e.message, error: true);
    return null;
  }
}

Future<void> addToPlaylistSheet(BuildContext context, String videoId) async {
  final api = context.api;
  final List<PlaylistSummary> playlists;
  try {
    playlists = await api.playlists();
  } on ApiException catch (e) {
    if (context.mounted) showToast(context, e.message, error: true);
    return;
  }
  if (!context.mounted) return;

  final choice = await showModalBottomSheet<String>(
    context: context,
    useSafeArea: true,
    builder: (context) => ListView(
      shrinkWrap: true,
      children: [
        const Padding(
          padding: EdgeInsets.fromLTRB(16, 0, 16, 8),
          child: Text('Adicionar à playlist', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800)),
        ),
        ListRow(
          leading: Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(color: AppColors.elevated2, borderRadius: BorderRadius.circular(4)),
            child: const Icon(Icons.add),
          ),
          title: 'Nova playlist',
          onTap: () => Navigator.pop(context, ''),
        ),
        for (final p in playlists)
          ListRow(
            leading: PlaylistCover(playlistId: p.id, coverVideoId: p.coverVideoId),
            title: p.name,
            subtitle: 'Playlist • ${p.ownerName}',
            onTap: () => Navigator.pop(context, p.id),
          ),
        const SizedBox(height: 16),
      ],
    ),
  );
  if (choice == null || !context.mounted) return;

  final playlistId = choice.isEmpty ? await createPlaylistFlow(context, open: false) : choice;
  if (playlistId == null || !context.mounted) return;
  try {
    await api.addToPlaylist(playlistId, videoId);
    notifyLibraryChanged();
    if (context.mounted) showToast(context, 'Adicionado à playlist');
  } on ApiException catch (e) {
    if (context.mounted) showToast(context, e.message, error: true);
  }
}
