import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:go_router/go_router.dart';

import 'app.dart';
import 'core/api.dart';
import 'core/session.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
  // On the web, keep the address bar in step with pushed screens (shareable /#/room/CODE links).
  GoRouter.optionURLReflectsImperativeAPIs = true;

  final session = await Session.load();
  final api = Api(session);

  // Resume a previous login. If the server is unreachable we just show the login screen.
  if (session.token != null && session.serverUrl != null) {
    try {
      session.restore(await api.me().timeout(const Duration(seconds: 6)));
    } on ApiException catch (e) {
      if (e.statusCode == 401) await session.signOut();
    } on TimeoutException {
      // keep the token; the user can retry from the login screen
    }
  }

  runApp(WatchPartyApp(session: session, api: api));
}
