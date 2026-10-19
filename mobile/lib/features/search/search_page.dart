import 'dart:async';

import 'package:flutter/material.dart';

import '../../core/api.dart';
import '../../core/app_scope.dart';
import '../../core/models.dart';
import '../../widgets/common.dart';

class SearchPage extends StatefulWidget {
  const SearchPage({super.key});

  @override
  State<SearchPage> createState() => _SearchPageState();
}

class _SearchPageState extends State<SearchPage> {
  final _controller = TextEditingController();
  Timer? _debounce;
  List<Video>? _results;
  String? _error;
  var _requestId = 0;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_results == null && _error == null) _search('');
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _controller.dispose();
    super.dispose();
  }

  Future<void> _search(String term) async {
    final id = ++_requestId;
    try {
      final results = await context.api.videos(search: term);
      if (mounted && id == _requestId) setState(() => (_results = results, _error = null));
    } on ApiException catch (e) {
      if (mounted && id == _requestId) setState(() => _error = e.message);
    }
  }

  @override
  Widget build(BuildContext context) {
    final term = _controller.text.trim();
    return Scaffold(
      body: SafeArea(
        bottom: false,
        child: CustomScrollView(
          slivers: [
            SliverToBoxAdapter(
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 16, 16, 8),
                child: Text('Buscar', style: Theme.of(context).textTheme.headlineMedium),
              ),
            ),
            SliverPersistentHeader(pinned: true, delegate: _SearchBar(_controller, (v) {
              _debounce?.cancel();
              _debounce = Timer(const Duration(milliseconds: 250), () => _search(v.trim()));
              setState(() {});
            })),
            if (_error != null)
              SliverToBoxAdapter(child: ErrorRetry(error: ApiException(_error!), onRetry: () => _search(term)))
            else if (_results == null)
              const SliverToBoxAdapter(child: Padding(padding: EdgeInsets.all(48), child: Center(child: CircularProgressIndicator())))
            else if (_results!.isEmpty)
              SliverToBoxAdapter(
                child: EmptyState(title: 'Nada encontrado para "$term"', message: 'Confira a ortografia ou tente outra palavra.'),
              )
            else ...[
              SliverToBoxAdapter(child: SectionHeader(term.isEmpty ? 'Navegar por tudo' : 'Resultados')),
              SliverPadding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 120),
                sliver: SliverGrid.builder(
                  gridDelegate: const SliverGridDelegateWithMaxCrossAxisExtent(
                    maxCrossAxisExtent: 260,
                    mainAxisSpacing: 16,
                    crossAxisSpacing: 12,
                    childAspectRatio: 1.15,
                  ),
                  itemCount: _results!.length,
                  itemBuilder: (_, i) => VideoCard(video: _results![i]),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _SearchBar extends SliverPersistentHeaderDelegate {
  _SearchBar(this.controller, this.onChanged);

  final TextEditingController controller;
  final ValueChanged<String> onChanged;

  @override
  double get minExtent => 64;

  @override
  double get maxExtent => 64;

  @override
  Widget build(BuildContext context, double shrinkOffset, bool overlapsContent) {
    return Container(
      color: Theme.of(context).scaffoldBackgroundColor,
      padding: const EdgeInsets.fromLTRB(16, 8, 16, 8),
      child: TextField(
        key: const Key('search-field'),
        controller: controller,
        onChanged: onChanged,
        style: const TextStyle(color: Colors.black, fontWeight: FontWeight.w600),
        cursorColor: Colors.black,
        decoration: InputDecoration(
          hintText: 'O que você quer assistir?',
          hintStyle: const TextStyle(color: Color(0xFF535353), fontWeight: FontWeight.w600),
          prefixIcon: const Icon(Icons.search, color: Colors.black),
          fillColor: Colors.white,
          contentPadding: EdgeInsets.zero,
          focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(6), borderSide: BorderSide.none),
        ),
      ),
    );
  }

  @override
  bool shouldRebuild(_SearchBar oldDelegate) => true;
}
