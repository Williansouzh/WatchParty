import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme.dart';

/// Bottom navigation like Spotify's mobile app: Início, Buscar, Biblioteca, Salas + Enviar.
class HomeShell extends StatelessWidget {
  const HomeShell({super.key, required this.shell});

  final StatefulNavigationShell shell;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: shell,
      extendBody: true,
      bottomNavigationBar: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [Color(0x00000000), Color(0xE6000000), AppColors.black],
            stops: [0, 0.35, 1],
          ),
        ),
        child: SafeArea(
          top: false,
          child: Padding(
            padding: const EdgeInsets.only(top: 18, bottom: 4),
            child: Row(
              children: [
                _item(context, 0, Icons.home_outlined, Icons.home_rounded, 'Início'),
                _item(context, 1, Icons.search, Icons.search, 'Buscar'),
                _item(context, 2, Icons.video_library_outlined, Icons.video_library, 'Biblioteca'),
                _item(context, 3, Icons.live_tv_outlined, Icons.live_tv, 'Salas'),
                Expanded(
                  child: _NavButton(
                    icon: Icons.add_circle_outline,
                    label: 'Enviar',
                    selected: false,
                    onTap: () => context.push('/upload'),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _item(BuildContext context, int index, IconData icon, IconData activeIcon, String label) {
    final selected = shell.currentIndex == index;
    return Expanded(
      child: _NavButton(
        icon: selected ? activeIcon : icon,
        label: label,
        selected: selected,
        onTap: () => shell.goBranch(index, initialLocation: selected),
      ),
    );
  }
}

class _NavButton extends StatelessWidget {
  const _NavButton({required this.icon, required this.label, required this.selected, required this.onTap});

  final IconData icon;
  final String label;
  final bool selected;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final color = selected ? AppColors.text : AppColors.textSub;
    return InkResponse(
      onTap: onTap,
      radius: 32,
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, color: color, size: 26),
          const SizedBox(height: 4),
          Text(label, style: TextStyle(fontSize: 11, color: color, fontWeight: selected ? FontWeight.w700 : FontWeight.w500)),
        ],
      ),
    );
  }
}
