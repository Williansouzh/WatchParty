import 'package:dio/dio.dart';
import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:wakelock_plus/wakelock_plus.dart';

import '../../core/api.dart';
import '../../core/app_scope.dart';
import '../../core/format.dart';
import '../../core/theme.dart';
import '../../widgets/actions.dart';

enum _Status { queued, uploading, done, failed }

class _Upload {
  _Upload(this.file, this.size) : title = TextEditingController(text: file.name.replaceFirst(RegExp(r'\.[^.]+$'), ''));

  final PlatformFile file;
  final int size;
  final TextEditingController title;
  final cancel = CancelToken();
  var status = _Status.queued;
  var progress = 0.0;
  String? message;
  String? videoId;
}

class UploadPage extends StatefulWidget {
  const UploadPage({super.key});

  @override
  State<UploadPage> createState() => _UploadPageState();
}

class _UploadPageState extends State<UploadPage> {
  final _items = <_Upload>[];
  var _running = false;

  bool get _busy => _items.any((i) => i.status == _Status.uploading || i.status == _Status.queued);

  @override
  void dispose() {
    for (final item in _items) {
      if (item.status == _Status.uploading) item.cancel.cancel();
      item.title.dispose();
    }
    WakelockPlus.disable();
    super.dispose();
  }

  Future<void> _pick() async {
    final files = await FilePicker.pickFiles(type: FileType.video);
    if (files.isEmpty) return;
    final added = <_Upload>[];
    for (final f in files) {
      added.add(_Upload(f, await f.length() ?? 0));
    }
    setState(() => _items.addAll(added));
    _process();
  }

  Future<void> _process() async {
    if (_running) return;
    _running = true;
    final api = context.api;
    await WakelockPlus.enable(); // keep the screen on so the upload isn't killed

    while (mounted) {
      final item = _items.where((i) => i.status == _Status.queued).firstOrNull;
      if (item == null) break;
      setState(() => item.status = _Status.uploading);
      final started = DateTime.now();
      try {
        item.videoId = await api.uploadVideo(
          file: item.file,
          title: item.title.text.trim().isEmpty ? item.file.name : item.title.text.trim(),
          cancelToken: item.cancel,
          onProgress: (p) {
            if (!mounted) return;
            final seconds = DateTime.now().difference(started).inMilliseconds / 1000;
            setState(() {
              item.progress = p;
              item.message = '${(p * 100).round()}% • ${formatBytes(seconds > 0 ? item.size * p / seconds : 0)}/s';
            });
          },
        );
        notifyLibraryChanged();
        if (mounted) setState(() => (item.status = _Status.done, item.progress = 1, item.message = 'Enviado! Convertendo no servidor…'));
      } on ApiException catch (e) {
        if (mounted) setState(() => (item.status = _Status.failed, item.message = e.message));
      }
    }

    _running = false;
    await WakelockPlus.disable();
  }

  @override
  Widget build(BuildContext context) {
    return PopScope(
      canPop: !_busy,
      onPopInvokedWithResult: (didPop, _) async {
        if (didPop) return;
        if (await confirmDialog(context, 'Cancelar envio?', 'Os envios em andamento serão interrompidos.', confirmLabel: 'Sair') &&
            context.mounted) {
          for (final i in _items) {
            if (i.status == _Status.queued) i.status = _Status.failed;
            if (i.status == _Status.uploading) i.cancel.cancel();
          }
          context.pop();
        }
      },
      child: Scaffold(
        appBar: AppBar(title: const Text('Enviar vídeos')),
        body: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            InkWell(
              key: const Key('pick-files'),
              borderRadius: BorderRadius.circular(12),
              onTap: _pick,
              child: Container(
                padding: const EdgeInsets.symmetric(vertical: 40, horizontal: 24),
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: const Color(0xFF535353), width: 2),
                ),
                child: const Column(
                  children: [
                    Icon(Icons.cloud_upload_outlined, size: 56),
                    SizedBox(height: 12),
                    Text('Escolher filmes e vídeos', style: TextStyle(fontSize: 18, fontWeight: FontWeight.w800)),
                    SizedBox(height: 6),
                    Text(
                      'MP4, MKV, MOV, AVI… O servidor converte para tocar em qualquer aparelho.',
                      textAlign: TextAlign.center,
                      style: TextStyle(color: AppColors.textSub),
                    ),
                  ],
                ),
              ),
            ),
            const SizedBox(height: 16),
            for (final item in _items) _UploadTile(item: item, onOpen: () => context.push('/video/${item.videoId}')),
          ],
        ),
      ),
    );
  }
}

class _UploadTile extends StatelessWidget {
  const _UploadTile({required this.item, required this.onOpen});

  final _Upload item;
  final VoidCallback onOpen;

  @override
  Widget build(BuildContext context) {
    final failed = item.status == _Status.failed;
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(color: AppColors.elevated, borderRadius: BorderRadius.circular(8)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          TextField(
            controller: item.title,
            enabled: item.status == _Status.queued,
            maxLength: 200,
            decoration: const InputDecoration(labelText: 'Título', counterText: '', isDense: true),
          ),
          const SizedBox(height: 10),
          ClipRRect(
            borderRadius: BorderRadius.circular(2),
            child: LinearProgressIndicator(
              value: item.status == _Status.queued ? 0 : item.progress,
              minHeight: 4,
              backgroundColor: const Color(0xFF4D4D4D),
              color: failed ? AppColors.danger : AppColors.accent,
            ),
          ),
          const SizedBox(height: 6),
          Row(
            children: [
              Expanded(
                child: Text(
                  item.message ?? '${item.file.name} • ${formatBytes(item.size)} • na fila',
                  style: TextStyle(color: failed ? AppColors.danger : AppColors.textSub, fontSize: 13),
                ),
              ),
              if (item.status == _Status.done && item.videoId != null)
                TextButton(onPressed: onOpen, child: const Text('Ver vídeo', style: TextStyle(color: AppColors.accent))),
            ],
          ),
        ],
      ),
    );
  }
}
