import 'package:flutter/material.dart';

import '../core/theme.dart';

/// Spotify "playlist page" layout: colored header with cover, big title, action row, then the list.
class CollectionView extends StatelessWidget {
  const CollectionView({
    super.key,
    required this.color,
    required this.cover,
    required this.kind,
    required this.title,
    this.description,
    required this.meta,
    required this.actions,
    required this.children,
    this.onRefresh,
    this.wideCover = false,
  });

  final Color color;
  final Widget cover;
  final String kind;
  final String title;
  final String? description;
  final String meta;
  final List<Widget> actions;
  final List<Widget> children;
  final Future<void> Function()? onRefresh;
  final bool wideCover;

  @override
  Widget build(BuildContext context) {
    final scroll = CustomScrollView(
      slivers: [
        SliverToBoxAdapter(
          child: DecoratedBox(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topCenter,
                end: Alignment.bottomCenter,
                colors: [color, Color.lerp(color, AppColors.background, 0.75)!, AppColors.background],
                stops: const [0, 0.75, 1],
              ),
            ),
            child: SafeArea(
              bottom: false,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 56, 16, 8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Center(
                      child: Container(
                        constraints: BoxConstraints(maxWidth: wideCover ? 520 : 240),
                        decoration: const BoxDecoration(boxShadow: [BoxShadow(color: Colors.black54, blurRadius: 40, offset: Offset(0, 8))]),
                        child: cover,
                      ),
                    ),
                    const SizedBox(height: 20),
                    Text(kind, style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600, color: Colors.white70)),
                    const SizedBox(height: 4),
                    Text(title, style: Theme.of(context).textTheme.headlineLarge),
                    if (description != null && description!.isNotEmpty) ...[
                      const SizedBox(height: 6),
                      Text(description!, style: const TextStyle(color: Colors.white70)),
                    ],
                    const SizedBox(height: 8),
                    Text(meta, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600, fontSize: 13)),
                    const SizedBox(height: 12),
                    Row(children: actions),
                  ],
                ),
              ),
            ),
          ),
        ),
        SliverPadding(
          padding: const EdgeInsets.only(bottom: 48),
          sliver: SliverList.list(children: children),
        ),
      ],
    );

    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: AppBar(
        leading: Padding(
          padding: const EdgeInsets.all(8),
          child: CircleAvatar(
            backgroundColor: Colors.black45,
            child: IconButton(
              padding: EdgeInsets.zero,
              icon: const Icon(Icons.arrow_back_ios_new, size: 18),
              onPressed: () => Navigator.of(context).maybePop(),
            ),
          ),
        ),
      ),
      body: onRefresh == null ? scroll : RefreshIndicator(onRefresh: onRefresh!, child: scroll),
    );
  }
}
