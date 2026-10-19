import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'core/api.dart';
import 'core/app_scope.dart';
import 'core/session.dart';
import 'core/theme.dart';
import 'features/auth/login_page.dart';
import 'features/home/home_page.dart';
import 'features/library/library_page.dart';
import 'features/library/liked_page.dart';
import 'features/playlist/playlist_page.dart';
import 'features/room/room_page.dart';
import 'features/rooms/rooms_page.dart';
import 'features/search/search_page.dart';
import 'features/shell/home_shell.dart';
import 'features/upload/upload_page.dart';
import 'features/video/video_page.dart';

class WatchPartyApp extends StatefulWidget {
  const WatchPartyApp({super.key, required this.session, required this.api});

  final Session session;
  final Api api;

  @override
  State<WatchPartyApp> createState() => _WatchPartyAppState();
}

class _WatchPartyAppState extends State<WatchPartyApp> {
  final _rootKey = GlobalKey<NavigatorState>();
  late final GoRouter _router = GoRouter(
    navigatorKey: _rootKey,
    initialLocation: '/home',
    refreshListenable: widget.session,
    redirect: (context, state) {
      final signedIn = widget.session.isSignedIn;
      final atLogin = state.matchedLocation == '/login';
      if (!signedIn && !atLogin) return '/login';
      if (signedIn && atLogin) return '/home';
      return null;
    },
    routes: [
      GoRoute(path: '/login', builder: (_, _) => const LoginPage()),
      StatefulShellRoute.indexedStack(
        builder: (_, _, shell) => HomeShell(shell: shell),
        branches: [
          StatefulShellBranch(routes: [GoRoute(path: '/home', builder: (_, _) => const HomePage())]),
          StatefulShellBranch(routes: [GoRoute(path: '/search', builder: (_, _) => const SearchPage())]),
          StatefulShellBranch(routes: [GoRoute(path: '/library', builder: (_, _) => const LibraryPage())]),
          StatefulShellBranch(routes: [GoRoute(path: '/rooms', builder: (_, _) => const RoomsPage())]),
        ],
      ),
      GoRoute(path: '/upload', parentNavigatorKey: _rootKey, builder: (_, _) => const UploadPage()),
      GoRoute(path: '/liked', parentNavigatorKey: _rootKey, builder: (_, _) => const LikedPage()),
      GoRoute(
        path: '/video/:id',
        parentNavigatorKey: _rootKey,
        builder: (_, state) => VideoPage(videoId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: '/playlist/:id',
        parentNavigatorKey: _rootKey,
        builder: (_, state) => PlaylistPage(playlistId: state.pathParameters['id']!),
      ),
      GoRoute(
        path: '/room/:code',
        parentNavigatorKey: _rootKey,
        builder: (_, state) => RoomPage(code: state.pathParameters['code']!.toUpperCase()),
      ),
    ],
  );

  @override
  void dispose() {
    _router.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AppScope(
      session: widget.session,
      api: widget.api,
      child: MaterialApp.router(
        title: 'WatchParty',
        debugShowCheckedModeBanner: false,
        theme: buildTheme(),
        darkTheme: buildTheme(),
        themeMode: ThemeMode.dark,
        routerConfig: _router,
      ),
    );
  }
}
