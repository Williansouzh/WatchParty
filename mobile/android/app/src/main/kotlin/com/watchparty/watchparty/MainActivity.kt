package com.watchparty.watchparty

import android.app.PictureInPictureParams
import android.content.pm.PackageManager
import android.content.res.Configuration
import android.os.Build
import android.util.Rational
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

/**
 * Adds picture-in-picture: while a room is open (Flutter calls "setEnabled"), leaving the app shrinks the
 * video to a floating window instead of stopping the session.
 */
class MainActivity : FlutterActivity() {
    private var pipEnabled = false
    private var channel: MethodChannel? = null

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
        channel = MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "watchparty/pip").also {
            it.setMethodCallHandler { call, result ->
                when (call.method) {
                    "isSupported" -> result.success(isPipSupported())
                    "setEnabled" -> {
                        pipEnabled = call.arguments as? Boolean ?: false
                        updateParams()
                        result.success(null)
                    }
                    "enter" -> {
                        if (isPipSupported()) enterPictureInPictureMode(params())
                        result.success(null)
                    }
                    else -> result.notImplemented()
                }
            }
        }
    }

    private fun isPipSupported() =
        Build.VERSION.SDK_INT >= Build.VERSION_CODES.O &&
            packageManager.hasSystemFeature(PackageManager.FEATURE_PICTURE_IN_PICTURE)

    private fun params(): PictureInPictureParams {
        val builder = PictureInPictureParams.Builder().setAspectRatio(Rational(16, 9))
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) builder.setAutoEnterEnabled(pipEnabled)
        return builder.build()
    }

    private fun updateParams() {
        if (isPipSupported()) setPictureInPictureParams(params())
    }

    // Android 8-11: no auto-enter, so do it when the user leaves (home button / recents).
    override fun onUserLeaveHint() {
        super.onUserLeaveHint()
        if (pipEnabled && isPipSupported() && Build.VERSION.SDK_INT < Build.VERSION_CODES.S) {
            enterPictureInPictureMode(params())
        }
    }

    override fun onPictureInPictureModeChanged(isInPictureInPictureMode: Boolean, newConfig: Configuration) {
        super.onPictureInPictureModeChanged(isInPictureInPictureMode, newConfig)
        channel?.invokeMethod("pipChanged", isInPictureInPictureMode)
    }
}
