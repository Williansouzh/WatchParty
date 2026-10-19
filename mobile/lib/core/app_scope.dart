import 'package:flutter/widgets.dart';

import 'api.dart';
import 'session.dart';

/// Makes the session and the API client available to every screen.
class AppScope extends InheritedWidget {
  const AppScope({super.key, required this.session, required this.api, required super.child});

  final Session session;
  final Api api;

  static AppScope of(BuildContext context) => context.dependOnInheritedWidgetOfExactType<AppScope>()!;

  @override
  bool updateShouldNotify(AppScope oldWidget) => session != oldWidget.session || api != oldWidget.api;
}

extension AppScopeContext on BuildContext {
  Api get api => AppScope.of(this).api;
  Session get session => AppScope.of(this).session;
}
