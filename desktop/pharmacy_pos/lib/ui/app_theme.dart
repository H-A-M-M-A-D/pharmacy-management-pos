import 'package:flutter/material.dart';

abstract final class AppColors {
  static const background = Color(0xFFF5F7F6);
  static const surface = Colors.white;
  static const primary = Color(0xFF167A5A);
  static const sidebar = Color(0xFF102A23);
  static const text = Color(0xFF18201D);
  static const muted = Color(0xFF66736D);
  static const border = Color(0xFFDCE3DF);
  static const successText = Color(0xFF147044);
  static const success = Color(0xFF198754);
  static const warning = Color(0xFF956600);
  static const danger = Color(0xFFD64545);
  static const dangerText = Color(0xFFB83232);
  static const informationText = Color(0xFF2463A5);
  static const information = Color(0xFF3478C5);
}

ThemeData buildAppTheme() {
  final base = ThemeData(useMaterial3: true, fontFamily: 'Inter');
  final text = base.textTheme.apply(
    bodyColor: AppColors.text,
    displayColor: AppColors.text,
  );
  const shape = RoundedRectangleBorder(
    borderRadius: BorderRadius.all(Radius.circular(8)),
  );
  return base.copyWith(
    colorScheme: ColorScheme.fromSeed(seedColor: AppColors.primary).copyWith(
      primary: AppColors.primary,
      onPrimary: Colors.white,
      surface: AppColors.surface,
      onSurface: AppColors.text,
      onSurfaceVariant: AppColors.muted,
      outline: AppColors.border,
      error: AppColors.danger,
      surfaceTint: Colors.transparent,
    ),
    scaffoldBackgroundColor: AppColors.background,
    textTheme: text.copyWith(
      headlineMedium: text.headlineMedium?.copyWith(
        fontSize: 24,
        fontWeight: FontWeight.w600,
      ),
      headlineSmall: text.headlineSmall?.copyWith(
        fontSize: 22,
        fontWeight: FontWeight.w600,
      ),
      titleLarge: text.titleLarge?.copyWith(
        fontSize: 18,
        fontWeight: FontWeight.w600,
      ),
      titleMedium: text.titleMedium?.copyWith(
        fontSize: 16,
        fontWeight: FontWeight.w600,
      ),
      bodyLarge: text.bodyLarge?.copyWith(fontSize: 14),
      bodyMedium: text.bodyMedium?.copyWith(fontSize: 14),
      bodySmall: text.bodySmall?.copyWith(fontSize: 12, color: AppColors.muted),
      labelLarge: text.labelLarge?.copyWith(
        fontSize: 13,
        fontWeight: FontWeight.w600,
      ),
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Colors.white,
      isDense: true,
      contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 13),
      floatingLabelBehavior: FloatingLabelBehavior.always,
      labelStyle: const TextStyle(fontSize: 13, color: AppColors.muted),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: AppColors.border),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: AppColors.border),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: AppColors.primary, width: 1.5),
      ),
    ),
    cardTheme: const CardThemeData(
      color: Colors.white,
      elevation: 0,
      margin: EdgeInsets.zero,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.all(Radius.circular(10)),
        side: BorderSide(color: AppColors.border),
      ),
    ),
    filledButtonTheme: FilledButtonThemeData(
      style: FilledButton.styleFrom(
        shape: shape,
        minimumSize: const Size(0, 40),
        padding: const EdgeInsets.symmetric(horizontal: 16),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        shape: shape,
        minimumSize: const Size(0, 40),
        side: const BorderSide(color: AppColors.border),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(shape: shape),
    ),
    iconButtonTheme: IconButtonThemeData(
      style: IconButton.styleFrom(shape: shape, iconSize: 20),
    ),
    dialogTheme: const DialogThemeData(
      backgroundColor: Colors.white,
      surfaceTintColor: Colors.transparent,
      elevation: 8,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.all(Radius.circular(12)),
      ),
      actionsPadding: EdgeInsets.fromLTRB(24, 12, 24, 20),
    ),
    dividerTheme: const DividerThemeData(
      color: AppColors.border,
      thickness: 1,
      space: 1,
    ),
    tabBarTheme: const TabBarThemeData(
      labelColor: AppColors.primary,
      unselectedLabelColor: AppColors.muted,
      indicatorSize: TabBarIndicatorSize.label,
      dividerColor: AppColors.border,
    ),
    snackBarTheme: SnackBarThemeData(
      behavior: SnackBarBehavior.floating,
      backgroundColor: AppColors.sidebar,
      shape: shape,
    ),
    dataTableTheme: DataTableThemeData(
      headingRowHeight: 42,
      dataRowMinHeight: 46,
      dataRowMaxHeight: 62,
      horizontalMargin: 16,
      columnSpacing: 20,
      headingRowColor: const WidgetStatePropertyAll(Color(0xFFF0F4F2)),
      headingTextStyle: const TextStyle(
        fontFamily: 'Inter',
        fontSize: 12,
        color: AppColors.muted,
        fontWeight: FontWeight.w600,
      ),
      dataTextStyle: const TextStyle(
        fontFamily: 'Inter',
        fontSize: 13,
        color: AppColors.text,
        fontFeatures: [FontFeature.tabularFigures()],
      ),
      dataRowColor: WidgetStateProperty.resolveWith(
        (states) => states.contains(WidgetState.selected)
            ? const Color(0xFFE7F2EC)
            : states.contains(WidgetState.hovered)
            ? const Color(0xFFF5F8F6)
            : Colors.white,
      ),
    ),
  );
}
