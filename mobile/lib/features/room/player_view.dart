import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:video_player/video_player.dart';

import '../../core/format.dart';
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
