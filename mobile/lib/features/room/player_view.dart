import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:video_player/video_player.dart';

import '../../core/format.dart';
import '../../core/models.dart';
import '../../core/theme.dart';
import 'room_controller.dart';

/// The synced player with Spotify-ish custom controls. Every control goes through the room controller.
class PlayerView extends StatefulWidget {
  const PlayerView({super.key, required this.room, this.fullscreen = false, this.onPickVideo});

  final RoomController room;
  final bool fullscreen;
  final VoidCallback? onPickVideo;

  @override
  State<PlayerView> createState() => _PlayerViewState();
}

class _PlayerViewState extends State<PlayerView> {
  var _controlsVisible = true;
  Timer? _hide;
  double? _dragValue;

  @override
  void initState() {
    super.initState();
    _scheduleHide();
  }

  @override
  void dispose() {
    _hide?.cancel();
    super.dispose();
  }

  void _scheduleHide() {
    _hide?.cancel();
    _hide = Timer(const Duration(seconds: 3), () {
      if (mounted && (widget.room.video?.value.isPlaying ?? false)) setState(() => _controlsVisible = false);
    });
  }

  void _toggleControls() {
    setState(() => _controlsVisible = !_controlsVisible);
    if (_controlsVisible) _scheduleHide();
  }

  Future<void> _openFullscreen() async {
    await Navigator.of(context).push(PageRouteBuilder<void>(
      opaque: true,
      pageBuilder: (_, _, _) => _FullscreenPlayer(room: widget.room),
      transitionsBuilder: (_, animation, _, child) => FadeTransition(opacity: animation, child: child),
    ));
  }

  @override
  Widget build(BuildContext context) {
    final room = widget.room;
    final controller = room.video;

    Widget content;
    if (controller == null) {
      content = Center(
        child: room.videoLoading
            ? const CircularProgressIndicator()
            : Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    room.videoError ??
                        (room.room?.currentVideo == null ? 'Nenhum vídeo na sala.' : 'Esse vídeo ainda está sendo preparado…'),
                    style: const TextStyle(color: AppColors.textSub),
                  ),
                  if (widget.onPickVideo != null && room.room?.currentVideo == null) ...[
                    const SizedBox(height: 12),
                    FilledButton.icon(onPressed: widget.onPickVideo, icon: const Icon(Icons.swap_horiz), label: const Text('Escolher vídeo')),
                  ],
                ],
              ),
      );
    } else {
      content = ValueListenableBuilder<VideoPlayerValue>(
        valueListenable: controller,
        builder: (context, value, _) {
          final duration = value.duration.inMilliseconds / 1000;
          final position = _dragValue ?? value.position.inMilliseconds / 1000;
          return GestureDetector(
            behavior: HitTestBehavior.opaque,
            onTap: _toggleControls,
            child: Stack(
              fit: StackFit.expand,
              children: [
                Center(
                  child: AspectRatio(
                    aspectRatio: value.aspectRatio > 0 ? value.aspectRatio : 16 / 9,
                    child: VideoPlayer(controller),
                  ),
                ),
                if (value.isBuffering) const Center(child: CircularProgressIndicator()),
                AnimatedOpacity(
                  opacity: _controlsVisible || !value.isPlaying ? 1 : 0,
                  duration: const Duration(milliseconds: 200),
                  child: IgnorePointer(
                    ignoring: !(_controlsVisible || !value.isPlaying),
                    child: DecoratedBox(
                      decoration: const BoxDecoration(
                        gradient: LinearGradient(
                          begin: Alignment.topCenter,
                          end: Alignment.bottomCenter,
                          colors: [Color(0x66000000), Color(0x22000000), Color(0xAA000000)],
                        ),
                      ),
                      child: Stack(
                        children: [
                          Center(
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                _RoundButton(icon: Icons.replay_10_rounded, label: 'Voltar 10 segundos', onTap: () => _act(() => room.seekBy(-10))),
                                const SizedBox(width: 28),
                                _RoundButton(
                                  key: const Key('play-pause'),
                                  icon: value.isPlaying ? Icons.pause_rounded : Icons.play_arrow_rounded,
                                  label: value.isPlaying ? 'Pausar' : 'Play',
                                  big: true,
                                  onTap: () => _act(room.togglePlay),
                                ),
                                const SizedBox(width: 28),
                                _RoundButton(icon: Icons.forward_10_rounded, label: 'Avançar 10 segundos', onTap: () => _act(() => room.seekBy(10))),
                              ],
                            ),
                          ),
                          Positioned(
                            left: 8,
                            right: 4,
                            bottom: 0,
                            child: Row(
                              children: [
                                Text(formatClock(position), style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600)),
                                Expanded(
                                  child: Slider(
                                    value: duration > 0 ? position.clamp(0, duration).toDouble() : 0,
                                    max: duration > 0 ? duration : 1,
                                    onChanged: duration > 0 ? (v) => setState(() => _dragValue = v) : null,
                                    onChangeEnd: (v) {
                                      setState(() => _dragValue = null);
                                      _act(() => room.seekTo(Duration(milliseconds: (v * 1000).round())));
                                    },
                                  ),
                                ),
                                Text(formatClock(duration), style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w600)),
                                if (room.subtitles.isNotEmpty)
                                  IconButton(
                                    tooltip: 'Legendas',
                                    icon: Icon(room.subtitle == null ? Icons.closed_caption_off_outlined : Icons.closed_caption),
                                    onPressed: () => _chooseSubtitle(context),
                                  ),
                                IconButton(
                                  tooltip: widget.fullscreen ? 'Sair da tela cheia' : 'Tela cheia',
                                  icon: Icon(widget.fullscreen ? Icons.fullscreen_exit : Icons.fullscreen),
                                  onPressed: widget.fullscreen ? () => Navigator.of(context).maybePop() : _openFullscreen,
                                ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
                ClosedCaption(text: value.caption.text, textStyle: const TextStyle(fontSize: 16, color: Colors.white)),
                if (room.waitingFor.isNotEmpty)
                  Positioned(
                    top: 10,
                    left: 0,
                    right: 0,
                    child: Center(child: _WaitingBanner(names: room.waitingFor)),
                  ),
                Positioned.fill(child: _ReactionLayer(stream: room.reactionStream)),
                if (room.needsTap)
                  Positioned.fill(
                    child: GestureDetector(
                      onTap: room.resumeFromTap,
                      child: Container(
                        color: Colors.black54,
                        alignment: Alignment.center,
                        child: FilledButton.icon(
                          onPressed: room.resumeFromTap,
                          icon: const Icon(Icons.play_arrow_rounded),
                          label: const Text('Toque para entrar na sessão'),
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          );
        },
      );
    }

    final player = ColoredBox(color: Colors.black, child: content);
    return widget.fullscreen ? player : AspectRatio(aspectRatio: 16 / 9, child: player);
  }

  Future<void> _chooseSubtitle(BuildContext context) async {
    final room = widget.room;
    final choice = await showModalBottomSheet<Object>(
      context: context,
      useSafeArea: true,
      builder: (context) => ListView(
        shrinkWrap: true,
        children: [
          const Padding(
            padding: EdgeInsets.fromLTRB(16, 0, 16, 8),
            child: Text('Legendas', style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800)),
          ),
          ListTile(
            leading: Icon(room.subtitle == null ? Icons.radio_button_checked : Icons.radio_button_off),
            title: const Text('Desligadas'),
            onTap: () => Navigator.pop(context, 'off'),
          ),
          for (final s in room.subtitles)
            ListTile(
              leading: Icon(room.subtitle?.id == s.id ? Icons.radio_button_checked : Icons.radio_button_off),
              title: Text(s.label),
              subtitle: s.language == null ? null : Text(s.language!),
              onTap: () => Navigator.pop(context, s),
            ),
          const SizedBox(height: 12),
        ],
      ),
    );
    if (choice == null) return;
    await room.selectSubtitle(choice is SubtitleInfo ? choice : null);
  }

  void _act(Future<void> Function() action) {
    _scheduleHide();
    action();
  }
}

class _RoundButton extends StatelessWidget {
  const _RoundButton({super.key, required this.icon, required this.label, required this.onTap, this.big = false});

  final IconData icon;
  final String label;
  final VoidCallback onTap;
  final bool big;

  @override
  Widget build(BuildContext context) {
    final size = big ? 64.0 : 44.0;
    return Semantics(
      button: true,
      label: label,
      excludeSemantics: true,
      child: Material(
        color: big ? Colors.white : Colors.black38,
        shape: const CircleBorder(),
        child: InkWell(
          customBorder: const CircleBorder(),
          onTap: onTap,
          child: SizedBox(width: size, height: size, child: Icon(icon, size: size * 0.6, color: big ? Colors.black : Colors.white)),
        ),
      ),
    );
  }
}

/// Landscape, immersive player. Uses the same controller, so sync keeps running.
class _FullscreenPlayer extends StatefulWidget {
  const _FullscreenPlayer({required this.room});

  final RoomController room;

  @override
  State<_FullscreenPlayer> createState() => _FullscreenPlayerState();
}

class _FullscreenPlayerState extends State<_FullscreenPlayer> {
  @override
  void initState() {
    super.initState();
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
    SystemChrome.setPreferredOrientations([DeviceOrientation.landscapeLeft, DeviceOrientation.landscapeRight]);
  }

  @override
  void dispose() {
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
    SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      body: ListenableBuilder(
        listenable: widget.room,
        builder: (_, _) {
          if (widget.room.status == RoomStatus.deleted) {
            WidgetsBinding.instance.addPostFrameCallback((_) => Navigator.of(context).maybePop());
          }
          return PlayerView(room: widget.room, fullscreen: true);
        },
      ),
    );
  }
}

class _WaitingBanner extends StatelessWidget {
  const _WaitingBanner({required this.names});

  final List<String> names;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
      decoration: BoxDecoration(color: Colors.black.withValues(alpha: 0.8), borderRadius: BorderRadius.circular(20)),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const SizedBox(width: 14, height: 14, child: CircularProgressIndicator(strokeWidth: 2)),
          const SizedBox(width: 10),
          Text('Esperando ${names.join(', ')} carregar…', style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
        ],
      ),
    );
  }
}

/// Emoji reactions floating up over the video.
class _ReactionLayer extends StatefulWidget {
  const _ReactionLayer({required this.stream});

  final Stream<Reaction> stream;

  @override
  State<_ReactionLayer> createState() => _ReactionLayerState();
}

class _ReactionLayerState extends State<_ReactionLayer> {
  final _items = <(Reaction, double)>[];
  StreamSubscription<Reaction>? _subscription;

  @override
  void initState() {
    super.initState();
    _subscription = widget.stream.listen((r) {
      if (!mounted) return;
      final left = 0.1 + (r.key.hashCode % 75) / 100;
      setState(() => _items.add((r, left)));
      Future.delayed(const Duration(milliseconds: 2600), () {
        if (mounted) setState(() => _items.removeWhere((i) => i.$1 == r));
      });
    });
  }

  @override
  void dispose() {
    _subscription?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return IgnorePointer(
      child: LayoutBuilder(
        builder: (context, constraints) => Stack(
          children: [
            for (final (reaction, left) in _items)
              Positioned(
                key: reaction.key,
                left: constraints.maxWidth * left,
                bottom: 40,
                child: TweenAnimationBuilder<double>(
                  tween: Tween(begin: 0, end: 1),
                  duration: const Duration(milliseconds: 2500),
                  curve: Curves.easeOut,
                  builder: (_, t, child) => Opacity(
                    opacity: t < 0.15 ? t / 0.15 : (1 - t).clamp(0, 1).toDouble(),
                    child: Transform.translate(offset: Offset(0, -constraints.maxHeight * 0.7 * t), child: child),
                  ),
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(reaction.emoji, style: const TextStyle(fontSize: 34)),
                      Text(reaction.displayName,
                          style: const TextStyle(fontSize: 10, fontWeight: FontWeight.w700, shadows: [Shadow(blurRadius: 4)])),
                    ],
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}
