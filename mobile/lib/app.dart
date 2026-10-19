import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import 'core/api.dart';
import 'core/app_scope.dart';
import 'core/lobby.dart';
import 'core/notifications.dart';
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
      // Remember where an invite link wanted to go, and continue there after logging in.
      if (!signedIn && !atLogin) {
        final target = state.uri.toString();
        return target == '/home' || target == '/' ? '/login' : '/login?from=${Uri.encodeComponent(target)}';
      }
      if (signedIn && atLogin) return state.uri.queryParameters['from'] ?? '/home';
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

  final _messenger = GlobalKey<ScaffoldMessengerState>();
  late final Lobby _lobby = Lobby(widget.session);
  StreamSubscription<RoomActivity>? _lobbySubscription;
  AppLifecycleListener? _lifecycle;
  var _inForeground = true;
  var _lobbyRunning = false;

  @override
  void initState() {
    super.initState();
    Notifications.init(onOpenRoom: (code) => _router.push('/room/$code'));
    _lifecycle = AppLifecycleListener(onStateChange: (s) => _inForeground = s == AppLifecycleState.resumed);
    _lobbySubscription = _lobby.activity.listen(_onRoomActivity);
    widget.session.addListener(_onSessionChanged);
    _onSessionChanged();
  }

  Future<void> _onSessionChanged() async {
    final signedIn = widget.session.isSignedIn;
    if (signedIn && !_lobbyRunning) {
      _lobbyRunning = true;
      await _lobby.start();
      await Notifications.requestPermission();
    } else if (!signedIn && _lobbyRunning) {
      _lobbyRunning = false;
      await _lobby.stop();
    }
  }

  /// A friend created or started a room: in-app banner when we're looking, system notification otherwise.
  void _onRoomActivity(RoomActivity activity) {
    final here = _router.routerDelegate.currentConfiguration.uri.path;
    if (here.toUpperCase().endsWith('/ROOM/${activity.code}')) return;

    if (_inForeground) {
      _messenger.currentState
        ?..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(
          content: Text(activity.text, style: const TextStyle(color: Colors.black, fontWeight: FontWeight.w600)),
          backgroundColor: AppColors.text,
          duration: const Duration(seconds: 8),
          action: SnackBarAction(label: 'Entrar', textColor: Colors.black, onPressed: () => _router.push('/room/${activity.code}')),
        ));
    } else {
      Notifications.roomActivity(code: activity.code, text: activity.text);
    }
  }

  @override
  void dispose() {
    widget.session.removeListener(_onSessionChanged);
    _lobbySubscription?.cancel();
    _lifecycle?.dispose();
    _lobby.stop();
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
        scaffoldMessengerKey: _messenger,
        theme: buildTheme(),
        darkTheme: buildTheme(),
        themeMode: ThemeMode.dark,
        routerConfig: _router,
      ),
    );
  }
}
