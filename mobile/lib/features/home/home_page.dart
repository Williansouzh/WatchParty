import 'package:flutter/foundation.dart';
import 'package:flutter/material.dart';
import 'package:package_info_plus/package_info_plus.dart';
import 'package:url_launcher/url_launcher.dart';
import 'package:go_router/go_router.dart';

import '../../core/api.dart';
import '../../core/app_scope.dart';
import '../../core/format.dart';
import '../../core/models.dart';
import '../../core/theme.dart';
import '../../widgets/actions.dart';
import '../../widgets/common.dart';

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  Future<(List<Video>, List<PlaylistSummary>, List<RoomSummary>)>? _data;

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

  Future<(List<Video>, List<PlaylistSummary>, List<RoomSummary>)> _load() async {
    final api = context.api;
    final results = await Future.wait([api.videos(take: 18), api.playlists(), api.rooms()]);
    return (results[0] as List<Video>, results[1] as List<PlaylistSummary>, results[2] as List<RoomSummary>);
  }

  @override
  Widget build(BuildContext context) {
    final user = context.session.user!;
    return Scaffold(
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [Color(0xFF2C4A36), AppColors.background],
            stops: [0, 0.35],
          ),
        ),
        child: SafeArea(
          bottom: false,
          child: RefreshIndicator(
            onRefresh: () async {
              _reload();
              await _data;
            },
            child: FutureBuilder(
              future: _data,
              builder: (context, snapshot) {
                final header = _Header(user: user);
                if (snapshot.hasError) {
                  return ListView(children: [header, ErrorRetry(error: snapshot.error!, onRetry: _reload)]);
                }
                if (!snapshot.hasData) {
                  return ListView(children: [header, const Padding(padding: EdgeInsets.all(48), child: Center(child: CircularProgressIndicator()))]);
                }
                final (videos, playlists, rooms) = snapshot.data!;
                final live = rooms.where((r) => r.onlineCount > 0).toList();
                final shownRooms = (live.isNotEmpty ? live : rooms).take(8).toList();

                return ListView(
                  padding: const EdgeInsets.only(bottom: 120),
                  children: [
                    header,
                    const _UpdateBanner(),
                    _Shortcuts(playlists: playlists),
                    if (shownRooms.isNotEmpty) ...[
                      SectionHeader(live.isNotEmpty ? 'Rolando agora' : 'Suas salas', onMore: () => context.go('/rooms')),
                      Carousel(children: [for (final r in shownRooms) RoomCard(room: r, width: 240)]),
                    ],
                    SectionHeader('Adicionados recentemente', onMore: videos.isEmpty ? null : () => context.go('/library')),
                    if (videos.isEmpty)
                      EmptyState(
                        title: 'Sua biblioteca está vazia',
                        message: 'Envie o primeiro filme e chame a galera.',
                        action: FilledButton.icon(
                          onPressed: () => context.push('/upload'),
                          icon: const Icon(Icons.upload_rounded),
                          label: const Text('Enviar vídeo'),
                        ),
                      )
                    else
                      Carousel(children: [for (final v in videos) VideoCard(video: v, width: 220)]),
                  ],
                );
              },
            ),
          ),
        ),
      ),
    );
  }
}

class _Header extends StatelessWidget {
  const _Header({required this.user});

  final AppUser user;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 16),
      child: Row(
        children: [
          PopupMenuButton<String>(
            tooltip: 'Conta',
            color: const Color(0xFF282828),
            onSelected: (v) => switch (v) {
              'password' => _changePassword(context),
              'everywhere' => _logoutEverywhere(context),
              _ => context.session.signOut(),
            },
            itemBuilder: (_) => [
              PopupMenuItem(enabled: false, child: Text('${user.displayName} (@${user.userName})')),
              const PopupMenuItem(value: 'password', child: Row(children: [Icon(Icons.password), SizedBox(width: 12), Text('Trocar senha')])),
              const PopupMenuItem(
                  value: 'everywhere', child: Row(children: [Icon(Icons.devices), SizedBox(width: 12), Text('Sair de todos os aparelhos')])),
              const PopupMenuItem(value: 'logout', child: Row(children: [Icon(Icons.logout), SizedBox(width: 12), Text('Sair')])),
            ],
            child: Avatar(name: user.displayName, color: user.avatarColor, size: 34),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text('${greeting()}, ${user.displayName.split(' ').first}',
                style: Theme.of(context).textTheme.headlineMedium, overflow: TextOverflow.ellipsis),
          ),
        ],
      ),
    );
  }
}

Future<void> _changePassword(BuildContext context) async {
  final values = await showFormDialog(context, title: 'Trocar senha', submitLabel: 'Trocar', fields: const [
    FormFieldSpec('current', 'Senha atual', required: true, maxLength: 128, obscure: true),
    FormFieldSpec('next', 'Nova senha (mínimo 6)', required: true, maxLength: 128, obscure: true),
  ]);
  if (values == null || !context.mounted) return;
  try {
    final token = await context.api.changePassword(values['current']!, values['next']!);
    if (!context.mounted) return;
    await context.session.replaceToken(token);
    if (context.mounted) showToast(context, 'Senha trocada. Os outros aparelhos foram desconectados.');
  } on ApiException catch (e) {
    if (context.mounted) showToast(context, e.message, error: true);
  }
}

Future<void> _logoutEverywhere(BuildContext context) async {
  if (!await confirmDialog(context, 'Sair de todos os aparelhos?',
      'Você vai precisar entrar de novo em todos os celulares e computadores, inclusive este.',
      confirmLabel: 'Sair de todos')) {
    return;
  }
  if (!context.mounted) return;
  final session = context.session;
  await context.api.logoutEverywhere().catchError((_) {});
  await session.signOut();
}

/// Shown when the server announces a newer Android build than the one installed.
class _UpdateBanner extends StatefulWidget {
  const _UpdateBanner();

  @override
  State<_UpdateBanner> createState() => _UpdateBannerState();
}

class _UpdateBannerState extends State<_UpdateBanner> {
  ({String? version, String url})? _update;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (kIsWeb || defaultTargetPlatform != TargetPlatform.android) return;
    _check();
  }

  Future<void> _check() async {
    try {
      final info = await context.api.appInfo();
      final package = await PackageInfo.fromPlatform();
      // Split APKs add 1000 × ABI to the version code; the build number is what's left.
      final installed = (int.tryParse(package.buildNumber) ?? 0) % 1000;
      if (info.latestBuild > installed && info.downloadUrl != null && mounted) {
        setState(() => _update = (version: info.latestVersion, url: info.downloadUrl!));
      }
    } catch (_) {/* no update info is fine */}
  }

  @override
  Widget build(BuildContext context) {
    final update = _update;
    if (update == null) return const SizedBox.shrink();
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 0, 16, 16),
      padding: const EdgeInsets.fromLTRB(14, 8, 8, 8),
      decoration: BoxDecoration(color: AppColors.accent, borderRadius: BorderRadius.circular(8)),
      child: Row(
        children: [
          const Icon(Icons.system_update, color: Colors.black),
          const SizedBox(width: 10),
          Expanded(
            child: Text('Nova versão${update.version == null ? '' : ' ${update.version}'} disponível',
                style: const TextStyle(color: Colors.black, fontWeight: FontWeight.w700)),
          ),
          TextButton(
            style: TextButton.styleFrom(backgroundColor: Colors.black, foregroundColor: Colors.white),
            onPressed: () => launchUrl(Uri.parse(update.url), mode: LaunchMode.externalApplication),
            child: const Text('Baixar'),
          ),
        ],
      ),
    );
  }
}

class _Shortcuts extends StatelessWidget {
  const _Shortcuts({required this.playlists});

  final List<PlaylistSummary> playlists;

  @override
  Widget build(BuildContext context) {
    final tiles = <Widget>[
      _Tile(leading: const LikedCover(size: 56), title: 'Vídeos curtidos', onTap: () => context.push('/liked')),
      for (final p in playlists.take(5))
        _Tile(
          leading: PlaylistCover(playlistId: p.id, coverVideoId: p.coverVideoId, size: 56),
          title: p.name,
          onTap: () => context.push('/playlist/${p.id}'),
        ),
      if (playlists.length < 5)
        _Tile(
          leading: const SizedBox(width: 56, height: 56, child: GradientPlaceholder(seed: 'rooms', icon: Icons.live_tv_rounded)),
          title: 'Salas',
          onTap: () => context.go('/rooms'),
        ),
    ];
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 16),
      child: GridView.count(
        crossAxisCount: 2,
        shrinkWrap: true,
        padding: EdgeInsets.zero,
        physics: const NeverScrollableScrollPhysics(),
        mainAxisSpacing: 8,
        crossAxisSpacing: 8,
        childAspectRatio: 3.1,
        children: tiles,
      ),
    );
  }
}

class _Tile extends StatelessWidget {
  const _Tile({required this.leading, required this.title, required this.onTap});

  final Widget leading;
  final String title;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Material(
      color: const Color(0x14FFFFFF),
      borderRadius: BorderRadius.circular(4),
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: onTap,
        child: Row(
          children: [
            leading,
            const SizedBox(width: 10),
            Expanded(
              child: Text(title,
                  maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)),
            ),
            const SizedBox(width: 6),
          ],
        ),
      ),
    );
  }
}
