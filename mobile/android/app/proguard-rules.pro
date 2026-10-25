# shared_preferences_android is called from Dart through JNI (package:jni), which looks classes and
# methods up by name, including Kotlin's coroutine runtime. Keep R8 from stripping or renaming them,
# otherwise the app hangs on the splash screen in release builds.
-keep class io.flutter.plugins.sharedpreferences.** { *; }
-keep class com.github.dart_lang.jni.** { *; }
-keep class kotlin.** { *; }
-keep class kotlinx.coroutines.** { *; }
