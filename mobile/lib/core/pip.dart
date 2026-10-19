import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';

/// Android picture-in-picture (see MainActivity.kt). A no-op on other platforms.
class Pip {
  Pip._();

  static const _channel = MethodChannel('watchparty/pip');
  static bool _initialized = false;

  /// True while the app is shown as a small floating window.
  static final active = ValueNotifier<bool>(false);

  static bool get _android => !kIsWeb && defaultTargetPlatform == TargetPlatform.android;

  static void _init() {
    if (_initialized || !_android) return;
    _initialized = true;
    _channel.setMethodCallHandler((call) async {
      if (call.method == 'pipChanged') active.value = call.arguments as bool;
    });
  }

  /// While enabled, leaving the app (home button) keeps the video playing in a floating window.
  static Future<void> setEnabled(bool enabled) async {
    if (!_android) return;
    _init();
    try {
      await _channel.invokeMethod('setEnabled', enabled);
    } on PlatformException catch (_) {
    } on MissingPluginException catch (_) {}
  }

  static Future<void> enter() async {
    if (!_android) return;
    _init();
    try {
      await _channel.invokeMethod('enter');
    } on PlatformException catch (_) {
    } on MissingPluginException catch (_) {}
  }
}
