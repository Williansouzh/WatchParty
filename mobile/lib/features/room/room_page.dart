import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:share_plus/share_plus.dart';
import 'package:wakelock_plus/wakelock_plus.dart';

import '../../core/app_scope.dart';
import '../../core/format.dart';
import '../../core/theme.dart';
import '../../widgets/actions.dart';
import '../../widgets/common.dart';
import 'player_view.dart';
import 'room_controller.dart';

class RoomPage extends StatefulWidget {
  const RoomPage({super.key, required this.code});

  final String code;

  @override
  State<RoomPage> createState() => _RoomPageState();
}

class _RoomPageState extends State<RoomPage> {
  RoomController? _room;
  StreamSubscription<String>? _toasts;
  AppLifecycleListener? _lifecycle;
  var _tab = 0; // 0 = chat, 1 = people
  final _message = TextEditingController();
  final _chatScroll = ScrollController();
  var _leaving = false;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_room != null) return;
    final room = RoomController(code: widget.code, session: context.session, api: context.api);
    _room = room;
    // Lifted above the chat input and the call bar so it never covers them.
    _toasts = room.toasts.listen((m) => mounted ? showToast(context, m, bottomOffset: 150) : null);
    room.addListener(_onRoomChanged);
    room.start();
    WakelockPlus.enable();
    _lifecycle = AppLifecycleListener(onResume: room.resync);
  }

  @override
  void dispose() {
    _lifecycle?.dispose();
    _toasts?.cancel();
    _room?.removeListener(_onRoomChanged);
    _room?.dispose();
    WakelockPlus.disable();
    _message.dispose();
    _chatScroll.dispose();
    super.dispose();
  }

  var _lastChatLength = 0;

  void _onRoomChanged() {
    final room = _room!;
    if (room.status == RoomStatus.deleted && !_leaving) {
      _leaving = true;
      showToast(context, 'A sala foi encerrada.');
      context.pop();
      return;
    }
    // Keep the chat pinned to the newest message.
    if (room.chat.length != _lastChatLength) {
      _lastChatLength = room.chat.length;
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (_chatScroll.hasClients) {
          _chatScroll.animateTo(_chatScroll.position.maxScrollExtent,
              duration: const Duration(milliseconds: 200), curve: Curves.easeOut);
        }
      });
    }
  }

  Future<void> _send() async {
    final text = _message.text.trim();
    if (text.isEmpty) return;
    _message.clear();
    if (!await _room!.sendMessage(text)) _message.text = text;
  }

  Future<void> _changeVideo() async {
    final video = await pickVideo(context, title: 'Trocar vídeo da sala');
    if (video != null) await _room!.changeVideo(video.id);
  }

  Future<void> _invite() async {
    final room = _room!.room!;
    final server = context.session.serverUrl;
    await SharePlus.instance.share(ShareParams(
      subject: room.name,
      text: 'Bora assistir junto? 🍿\n'
          'Sala "${room.name}" • código ${room.code}\n'
          'No app: Salas → Entrar com o código\n'
          'No navegador: $server/#/room/${room.code}',
    ));
  }

  Future<void> _deleteRoom() async {
    if (await confirmDialog(context, 'Encerrar sala?', 'Todo mundo será desconectado e o chat será apagado.', confirmLabel: 'Encerrar')) {
      await _room!.deleteRoom();
    }
  }

  @override
  Widget build(BuildContext context) {
    final room = _room!;
    return ListenableBuilder(
      listenable: room,
      builder: (context, _) {
        if (room.status == RoomStatus.connecting && room.room == null) {
          return Scaffold(appBar: AppBar(), body: const Center(child: CircularProgressIndicator()));
        }
        if (room.room == null) {
          return Scaffold(
            appBar: AppBar(),
            body: EmptyState(
              title: 'Sala não encontrada',
              message: room.error,
              action: FilledButton(onPressed: () => context.pop(), child: const Text('Voltar')),
            ),
          );
        }
        final info = room.room!;

        return Scaffold(
          backgroundColor: AppColors.black,
          body: SafeArea(
            child: Column(
              children: [
                _TopBar(title: info.name, status: room.status, onBack: () => context.pop()),
                PlayerView(room: room, onPickVideo: _changeVideo),
                Padding(
                  padding: const EdgeInsets.fromLTRB(16, 12, 8, 4),
                  child: Row(
                    children: [
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(info.currentVideo?.title ?? 'Sem vídeo',
                                maxLines: 1, overflow: TextOverflow.ellipsis, style: Theme.of(context).textTheme.titleMedium),
                            const SizedBox(height: 2),
                            Row(
                              children: [
                                Container(
                                  padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 1),
                                  decoration: BoxDecoration(color: AppColors.elevated2, borderRadius: BorderRadius.circular(4)),
                                  child: Text(info.code,
                                      style: const TextStyle(fontFamily: 'monospace', letterSpacing: 2, fontWeight: FontWeight.w700, fontSize: 12)),
                                ),
                                const SizedBox(width: 8),
                                const Icon(Icons.circle, size: 8, color: AppColors.accent),
                                const SizedBox(width: 4),
                                Text('${room.watchingCount} assistindo', style: Theme.of(context).textTheme.bodySmall),
                              ],
                            ),
                          ],
                        ),
                      ),
                      IconButton(tooltip: 'Convidar', onPressed: _invite, icon: const Icon(Icons.ios_share_rounded)),
                      IconButton(tooltip: 'Trocar vídeo', onPressed: _changeVideo, icon: const Icon(Icons.swap_horiz_rounded)),
                      if (room.isHost)
                        IconButton(tooltip: 'Encerrar sala', onPressed: _deleteRoom, icon: const Icon(Icons.delete_outline, color: AppColors.danger)),
                    ],
                  ),
                ),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 4),
                  child: Row(
                    children: [
                      _TabChip(
                        label: 'Chat',
                        icon: Icons.chat_bubble_outline,
                        selected: _tab == 0,
                        badge: room.unread,
                        onTap: () {
                          setState(() => _tab = 0);
                          room.setChatVisible(true);
                        },
                      ),
                      const SizedBox(width: 8),
                      _TabChip(
                        label: 'Pessoas (${room.watchingCount})',
                        icon: Icons.group_outlined,
                        selected: _tab == 1,
                        onTap: () {
                          setState(() => _tab = 1);
                          room.setChatVisible(false);
                        },
                      ),
                    ],
                  ),
                ),
                Expanded(
                  child: Container(
                    margin: const EdgeInsets.fromLTRB(8, 4, 8, 0),
                    decoration: BoxDecoration(color: AppColors.background, borderRadius: BorderRadius.circular(8)),
                    child: _tab == 0 ? _ChatList(room: room, scroll: _chatScroll) : _PeopleList(room: room),
                  ),
                ),
                if (_tab == 0) _ChatInput(controller: _message, onSend: _send),
                _VoiceBar(room: room),
              ],
            ),
          ),
        );
      },
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({required this.title, required this.status, required this.onBack});

  final String title;
  final RoomStatus status;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 48,
      child: Row(
        children: [
          IconButton(icon: const Icon(Icons.keyboard_arrow_down_rounded, size: 30), onPressed: onBack, tooltip: 'Sair da sala'),
          Expanded(
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                Text(
                  status == RoomStatus.reconnecting ? 'RECONECTANDO…' : 'ASSISTINDO NA SALA',
                  style: TextStyle(
                    fontSize: 10,
                    letterSpacing: 1,
                    fontWeight: FontWeight.w600,
                    color: status == RoomStatus.reconnecting ? AppColors.danger : AppColors.textSub,
                  ),
                ),
                Text(title, maxLines: 1, overflow: TextOverflow.ellipsis, style: const TextStyle(fontWeight: FontWeight.w700)),
              ],
            ),
          ),
          const SizedBox(width: 48),
        ],
      ),
    );
  }
}

class _TabChip extends StatelessWidget {
  const _TabChip({required this.label, required this.icon, required this.selected, required this.onTap, this.badge = 0});

  final String label;
  final IconData icon;
  final bool selected;
  final VoidCallback onTap;
  final int badge;

  @override
  Widget build(BuildContext context) {
    final fg = selected ? Colors.black : AppColors.text;
    return Material(
      color: selected ? AppColors.text : AppColors.elevated2,
      shape: const StadiumBorder(),
      child: InkWell(
        customBorder: const StadiumBorder(),
        onTap: onTap,
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
          child: Row(
            children: [
              Icon(icon, size: 16, color: fg),
              const SizedBox(width: 6),
              Text(label, style: TextStyle(color: fg, fontWeight: FontWeight.w600)),
              if (badge > 0) ...[
                const SizedBox(width: 6),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 6),
                  decoration: BoxDecoration(color: AppColors.accent, borderRadius: BorderRadius.circular(10)),
                  child: Text('$badge', style: const TextStyle(color: Colors.black, fontSize: 12, fontWeight: FontWeight.w700)),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _ChatList extends StatelessWidget {
  const _ChatList({required this.room, required this.scroll});

  final RoomController room;
  final ScrollController scroll;

  @override
  Widget build(BuildContext context) {
    final entries = room.chat;
    return ListView.builder(
      controller: scroll,
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      itemCount: entries.length,
      itemBuilder: (_, i) {
        final entry = entries[i];
        switch (entry) {
          case SystemEntry(:final text):
            return Padding(
              padding: const EdgeInsets.symmetric(vertical: 6),
              child: Text(text, textAlign: TextAlign.center, style: const TextStyle(color: AppColors.textMuted, fontSize: 12)),
            );
          case MessageEntry(:final message):
            final previous = i > 0 ? entries[i - 1] : null;
            final grouped = previous is MessageEntry &&
                previous.message.userId == message.userId &&
                message.sentAt.difference(previous.message.sentAt).inMinutes < 3;
            return Padding(
              padding: EdgeInsets.only(top: grouped ? 2 : 10),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  SizedBox(
                    width: 30,
                    child: grouped ? null : Avatar(name: message.displayName, color: message.avatarColor, size: 28),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        if (!grouped)
                          Text.rich(TextSpan(children: [
                            TextSpan(text: message.displayName, style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 13)),
                            TextSpan(
                                text: '  ${timeOfDay(message.sentAt)}',
                                style: const TextStyle(color: AppColors.textMuted, fontSize: 11)),
                          ])),
                        Text(message.content, style: const TextStyle(color: Color(0xFFE6E6E6))),
                      ],
                    ),
                  ),
                ],
              ),
            );
        }
      },
    );
  }
}

class _PeopleList extends StatelessWidget {
  const _PeopleList({required this.room});

  final RoomController room;

  @override
  Widget build(BuildContext context) {
    final people = room.participants.values.toList()
      ..sort((a, b) => a.inVoice == b.inVoice ? a.displayName.compareTo(b.displayName) : (a.inVoice ? -1 : 1));
    return ListView(
      padding: const EdgeInsets.symmetric(vertical: 8),
      children: [
        for (final p in people)
          ListTile(
            leading: Avatar(name: p.displayName, color: p.avatarColor, size: 36, speaking: room.isSpeaking(p.connectionId)),
            title: Text('${p.displayName}${p.connectionId == room.myConnectionId ? ' (você)' : ''}',
                style: const TextStyle(fontWeight: FontWeight.w600)),
            subtitle: Text(p.inVoice ? (p.isMuted ? 'Na call • mutado' : 'Na call') : 'Assistindo',
                style: const TextStyle(color: AppColors.textSub)),
            trailing: p.inVoice ? Icon(p.isMuted ? Icons.mic_off : Icons.mic, color: AppColors.textSub, size: 20) : null,
          ),
      ],
    );
  }
}

class _ChatInput extends StatelessWidget {
  const _ChatInput({required this.controller, required this.onSend});

  final TextEditingController controller;
  final VoidCallback onSend;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(8, 8, 8, 4),
      child: Row(
        children: [
          Expanded(
            child: TextField(
              key: const Key('chat-input'),
              controller: controller,
              maxLength: 1000,
              textInputAction: TextInputAction.send,
              onSubmitted: (_) => onSend(),
              decoration: InputDecoration(
                hintText: 'Mande uma mensagem',
                counterText: '',
                isDense: true,
                border: OutlineInputBorder(borderRadius: BorderRadius.circular(24), borderSide: BorderSide.none),
                focusedBorder: OutlineInputBorder(borderRadius: BorderRadius.circular(24), borderSide: BorderSide.none),
                contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              ),
            ),
          ),
          const SizedBox(width: 8),
          PlayFab(onPressed: onSend, size: 44, icon: Icons.send_rounded, label: 'Enviar mensagem'),
        ],
      ),
    );
  }
}

/// Spotify's "now playing" bar, used here for the voice call.
class _VoiceBar extends StatelessWidget {
  const _VoiceBar({required this.room});

  final RoomController room;

  @override
  Widget build(BuildContext context) {
    final voice = room.voice;
    final inCall = voice?.active ?? false;
    final callers = room.voiceParticipants;
    final video = room.room?.currentVideo;

    return Container(
      margin: const EdgeInsets.fromLTRB(8, 4, 8, 8),
      padding: const EdgeInsets.all(8),
      decoration: BoxDecoration(color: AppColors.elevated2, borderRadius: BorderRadius.circular(8)),
      child: Row(
        children: [
          video == null
              ? const SizedBox(width: 40, height: 40, child: GradientPlaceholder(seed: 'room', icon: Icons.live_tv_rounded))
              : SizedBox(width: 40, height: 40, child: ClipRRect(borderRadius: BorderRadius.circular(4), child: FittedBox(
                  fit: BoxFit.cover, child: SizedBox(width: 71, height: 40, child: VideoThumb.of(video, radius: 0))))),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(inCall ? 'Na call' : 'Call de voz', style: const TextStyle(fontWeight: FontWeight.w700)),
                Row(
                  children: [
                    Text(
                      callers.isEmpty ? 'Ninguém na call' : plural(callers.length, 'pessoa', 'pessoas'),
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                    const SizedBox(width: 6),
                    for (final p in callers.take(5))
                      Padding(
                        padding: const EdgeInsets.only(right: 2),
                        child: Avatar(
                          name: p.displayName,
                          color: p.avatarColor,
                          size: 18,
                          speaking: room.isSpeaking(p.connectionId),
                          dimmed: p.isMuted,
                        ),
                      ),
                  ],
                ),
              ],
            ),
          ),
          if (!inCall)
            FilledButton.icon(
              key: const Key('join-voice'),
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.text,
                minimumSize: const Size(0, 38),
                padding: const EdgeInsets.symmetric(horizontal: 14),
              ),
              onPressed: room.joinVoice,
              icon: const Icon(Icons.headset_mic_rounded, size: 20),
              label: const Text('Entrar', style: TextStyle(fontSize: 14)),
            )
          else ...[
            _CircleButton(
              key: const Key('mute'),
              label: voice!.muted ? 'Ativar microfone' : 'Mutar',
              icon: voice.muted ? Icons.mic_off_rounded : Icons.mic_rounded,
              background: voice.muted ? AppColors.text : AppColors.elevated,
              foreground: voice.muted ? Colors.black : AppColors.text,
              onTap: room.toggleMute,
            ),
            const SizedBox(width: 8),
            _CircleButton(
              key: const Key('leave-voice'),
              label: 'Sair da call',
              icon: Icons.call_end_rounded,
              background: const Color(0xFFE9142A),
              foreground: Colors.white,
              onTap: room.leaveVoice,
            ),
          ],
        ],
      ),
    );
  }
}

class _CircleButton extends StatelessWidget {
  const _CircleButton({
    super.key,
    required this.icon,
    required this.label,
    required this.background,
    required this.foreground,
    required this.onTap,
  });

  final IconData icon;
  final String label;
  final Color background;
  final Color foreground;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: label,
      excludeSemantics: true,
      child: Material(
        color: background,
        shape: const CircleBorder(),
        child: InkWell(
          customBorder: const CircleBorder(),
          onTap: onTap,
          child: SizedBox(width: 42, height: 42, child: Icon(icon, color: foreground)),
        ),
      ),
    );
  }
}
