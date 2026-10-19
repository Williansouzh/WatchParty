import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../core/api.dart';
import '../../core/app_scope.dart';
import '../../core/models.dart';
import '../../widgets/actions.dart';
import '../../widgets/common.dart';

class RoomsPage extends StatefulWidget {
  const RoomsPage({super.key});

  @override
  State<RoomsPage> createState() => _RoomsPageState();
}

class _RoomsPageState extends State<RoomsPage> {
  Future<List<RoomSummary>>? _rooms;
  final _code = TextEditingController();

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _rooms ??= context.api.rooms();
  }

  @override
  void dispose() {
    _code.dispose();
    super.dispose();
  }

  void _reload() => setState(() => _rooms = context.api.rooms());

  Future<void> _create() async {
    final values = await showFormDialog(
      context,
      title: 'Criar sala',
      submitLabel: 'Escolher vídeo',
      fields: const [FormFieldSpec('name', 'Nome da sala', initial: 'Sessão de cinema', maxLength: 80, required: true)],
    );
    if (values == null || !mounted) return;
    final video = await pickVideo(context, title: 'O que vamos assistir?');
    if (!mounted) return;
    try {
      final code = await context.api.createRoom(values['name']!, videoId: video?.id);
      if (mounted) await context.push('/room/$code');
      _reload();
    } on ApiException catch (e) {
      if (mounted) showToast(context, e.message, error: true);
    }
  }

  Future<void> _join() async {
    final code = _code.text.trim().toUpperCase();
    if (code.isEmpty) return;
    _code.clear();
    await context.push('/room/$code');
    _reload();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: RefreshIndicator(
          onRefresh: () async {
            _reload();
            await _rooms;
          },
          child: FutureBuilder(
            future: _rooms,
            builder: (context, snapshot) {
              final header = Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Salas', style: Theme.of(context).textTheme.headlineMedium),
                    const SizedBox(height: 16),
                    FilledButton.icon(
                      key: const Key('create-room'),
                      onPressed: _create,
                      icon: const Icon(Icons.add),
                      label: const Text('Criar sala'),
                    ),
                    const SizedBox(height: 12),
                    Row(
                      children: [
                        Expanded(
                          child: TextField(
                            controller: _code,
                            maxLength: 6,
                            textCapitalization: TextCapitalization.characters,
                            onSubmitted: (_) => _join(),
                            decoration: const InputDecoration(hintText: 'Código (ex: K7P2QX)', counterText: ''),
                          ),
                        ),
                        const SizedBox(width: 8),
                        OutlinedButton(onPressed: _join, child: const Text('Entrar')),
                      ],
                    ),
                  ],
                ),
              );

              if (snapshot.hasError) return ListView(children: [header, ErrorRetry(error: snapshot.error!, onRetry: _reload)]);
              if (!snapshot.hasData) {
                return ListView(children: [header, const Padding(padding: EdgeInsets.all(48), child: Center(child: CircularProgressIndicator()))]);
              }
              final rooms = snapshot.data!;
              return CustomScrollView(
                slivers: [
                  SliverToBoxAdapter(child: header),
                  if (rooms.isEmpty)
                    const SliverToBoxAdapter(
                      child: EmptyState(
                        title: 'Nenhuma sala ainda',
                        message: 'Crie uma sala, escolha um filme e mande o código pros amigos.',
                      ),
                    )
                  else
                    SliverPadding(
                      padding: const EdgeInsets.fromLTRB(16, 16, 16, 120),
                      sliver: SliverGrid.builder(
                        gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
                          maxCrossAxisExtent: 320,
                          mainAxisSpacing: 16,
                          crossAxisSpacing: 12,
                          childAspectRatio: 1.25,
                        ),
                        itemCount: rooms.length,
                        itemBuilder: (_, i) => RoomCard(room: rooms[i]),
                      ),
                    ),
                ],
              );
            },
          ),
        ),
      ),
    );
  }
}
