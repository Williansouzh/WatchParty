import 'package:flutter/material.dart';

/// Spotify-like dark palette.
abstract final class AppColors {
  static const black = Color(0xFF000000);
  static const background = Color(0xFF121212);
  static const elevated = Color(0xFF1F1F1F);
  static const elevated2 = Color(0xFF2A2A2A);
  static const text = Color(0xFFFFFFFF);
  static const textSub = Color(0xFFB3B3B3);
  static const textMuted = Color(0xFF7C7C7C);
  static const accent = Color(0xFF1ED760);
  static const danger = Color(0xFFF3727F);
  static const liked1 = Color(0xFF450AF5);
  static const liked2 = Color(0xFFC4EFD9);
}

ThemeData buildTheme() {
  final base = ThemeData(
    useMaterial3: true,
    brightness: Brightness.dark,
    colorScheme: const ColorScheme.dark(
      primary: AppColors.accent,
      onPrimary: Colors.black,
      secondary: AppColors.accent,
      surface: AppColors.background,
      onSurface: AppColors.text,
      error: AppColors.danger,
    ),
    scaffoldBackgroundColor: AppColors.background,
  );

  return base.copyWith(
    textTheme: base.textTheme
        .apply(bodyColor: AppColors.text, displayColor: AppColors.text)
        .copyWith(
          headlineLarge: const TextStyle(color: AppColors.text, fontSize: 30, fontWeight: FontWeight.w900, letterSpacing: -1),
          headlineMedium: const TextStyle(color: AppColors.text, fontSize: 24, fontWeight: FontWeight.w800, letterSpacing: -0.5),
          titleLarge: const TextStyle(color: AppColors.text, fontSize: 20, fontWeight: FontWeight.w800, letterSpacing: -0.3),
          titleMedium: const TextStyle(color: AppColors.text, fontSize: 16, fontWeight: FontWeight.w700),
          bodyMedium: const TextStyle(fontSize: 14, color: AppColors.text),
          bodySmall: const TextStyle(fontSize: 13, color: AppColors.textSub),
        ),
    appBarTheme: const AppBarTheme(
      backgroundColor: Colors.transparent,
      elevation: 0,
      scrolledUnderElevation: 0,
      centerTitle: false,
      titleTextStyle: TextStyle(fontSize: 18, fontWeight: FontWeight.w800, color: AppColors.text),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        backgroundColor: AppColors.accent,
        foregroundColor: Colors.black,
        minimumSize: const Size(64, 48),
        textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w800),
        shape: const StadiumBorder(),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: AppColors.text,
        side: const BorderSide(color: AppColors.textMuted),
        minimumSize: const Size(64, 40),
        textStyle: const TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
        shape: const StadiumBorder(),
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: AppColors.elevated2,
      hintStyle: const TextStyle(color: AppColors.textMuted),
      labelStyle: const TextStyle(color: AppColors.textSub),
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(6), borderSide: BorderSide.none),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(6),
        borderSide: const BorderSide(color: AppColors.text, width: 1.5),
      ),
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 14),
    ),
    chipTheme: base.chipTheme.copyWith(
      backgroundColor: AppColors.elevated2,
      selectedColor: AppColors.text,
      labelStyle: const TextStyle(fontWeight: FontWeight.w600),
      side: BorderSide.none,
      shape: const StadiumBorder(),
      showCheckmark: false,
    ),
    snackBarTheme: const SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      backgroundColor: AppColors.text,
      contentTextStyle: TextStyle(color: Colors.black, fontWeight: FontWeight.w600),
    ),
    bottomSheetTheme: const BottomSheetThemeData(backgroundColor: AppColors.elevated, showDragHandle: true),
    dialogTheme: const DialogThemeData(backgroundColor: Color(0xFF282828)),
    sliderTheme: const SliderThemeData(
      activeTrackColor: AppColors.text,
      inactiveTrackColor: Color(0x4DFFFFFF),
      thumbColor: AppColors.text,
      trackHeight: 3,
      overlayShape: RoundSliderOverlayShape(overlayRadius: 14),
      thumbShape: RoundSliderThumbShape(enabledThumbRadius: 6),
    ),
    progressIndicatorTheme: const ProgressIndicatorThemeData(color: AppColors.accent),
  );
}
