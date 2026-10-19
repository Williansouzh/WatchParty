import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../core/app_scope.dart';
import '../../core/models.dart';
import '../../core/theme.dart';
import '../../widgets/actions.dart';
import '../../widgets/common.dart';

enum _Filter { all, playlists, favorites, mine }

class LibraryPage extends StatefulWidget {
  const LibraryPage({super.key});

  @override
  State<LibraryPage> createState() => _LibraryPageState();
}

class _LibraryPageState extends State<LibraryPage> {
  var _filter = _Filter.all;
  Future<(List<Video>, List<PlaylistSummary>)>? _data;

  @override
  void initState() {
    super.initState();
    libraryVersion.addListener(_reload);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _data ??= _load();
  }

  @override
  void dispose() {
    libraryVersion.removeListener(_reload);
    super.dispose();
  }

  void _reload() => setState(() => _data = _load());

  Future<(List<Video>, List<PlaylistSummary>)> _load() async {
    final api = context.api;
    final filter = switch (_filter) {
      _Filter.favorites => 'favorites',
      _Filter.mine => 'mine',
      _ => null,
    };
    final videos = _filter == _Filter.playlists ? Future.value(<Video>[]) : api.videos(filter: filter);
    return (await videos, await api.playlists());
  }

  void _select(_Filter f) {
    setState(() {
      _filter = _filter == f ? _Filter.all : f;
      _data = _load();
    });
  }

  @override
  Widget build(BuildContext context) {
    final user = context.session.user!;
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 8, 8),
              child: Row(
                children: [
                  Avatar(name: user.displayName, color: user.avatarColor, size: 32),
                  const SizedBox(width: 12),
                  Expanded(child: Text('Sua Biblioteca', style: Theme.of(context).textTheme.headlineMedium)),
                  IconButton(
                    tooltip: 'Criar playlist',
                    onPressed: () => createPlaylistFlow(context),
                    icon: const Icon(Icons.add, size: 28),
                  ),
                ],
              ),
            ),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              padding: const EdgeInsets.symmetric(horizontal: 16),
              child: Row(
                children: [
                  for (final (f, label) in [
                    (_Filter.playlists, 'Playlists'),
                    (_Filter.favorites, 'Curtidos'),
                    (_Filter.mine, 'Meus envios'),
                  ])
                    Padding(
                      padding: const EdgeInsets.only(right: 8),
                      child: ChoiceChip(
                        label: Text(label, style: TextStyle(color: _filter == f ? Colors.black : AppColors.text)),
                        selected: _filter == f,
                        onSelected: (_) => _select(f),
                      ),
                    ),
                ],
              ),
            ),
            const SizedBox(height: 8),
            Expanded(
              child: RefreshIndicator(
                onRefresh: () async {
                  _reload();
                  await _data;
                },
                child: FutureBuilder(
                  future: _data,
                  builder: (context, snapshot) {
                    if (snapshot.hasError) return ListView(children: [ErrorRetry(error: snapshot.error!, onRetry: _reload)]);
                    if (!snapshot.hasData) return const Center(child: CircularProgressIndicator());
                    final (videos, playlists) = snapshot.data!;
                    final showPlaylists = _filter == _Filter.all || _filter == _Filter.playlists;

                    final rows = <Widget>[
                      if (showPlaylists) ...[
                        ListRow(
                          leading: const LikedCover(size: 56),
                          title: 'Vídeos curtidos',
                          subtitle: 'Coleção',
                          onTap: () => context.push('/liked'),
                        ),
                        for (final p in playlists)
                          ListRow(
                            leading: PlaylistCover(playlistId: p.id, coverVideoId: p.coverVideoId, size: 56),
                            title: p.name,
                            subtitle: 'Playlist • ${p.ownerName} • ${p.itemCount} vídeo${p.itemCount == 1 ? '' : 's'}',
                            onTap: () => context.push('/playlist/${p.id}'),
                          ),
                      ],
                      for (final v in videos)
                        ListRow(
                          leading: VideoThumb.of(v, width: 100, radius: 4),
                          title: v.title,
                          subtitle: 'Vídeo • ${videoSubtitle(v)}',
                          onTap: () => context.push('/video/${v.id}'),
                        ),
                    ];

                    if (rows.isEmpty) {
                      return ListView(children: [
                        EmptyState(
                          title: _filter == _Filter.favorites ? 'Nenhum vídeo curtido' : 'Nada por aqui ainda',
                          message: _filter == _Filter.favorites
                              ? 'Toque no coração de um vídeo para salvar aqui.'
                              : 'Envie um vídeo para começar.',
                        ),
                      ]);
                    }
                    return ListView(padding: const EdgeInsets.only(bottom: 120), children: rows);
                  },
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
