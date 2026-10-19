import 'dart:async';
import 'dart:convert';

import 'package:web_socket_channel/web_socket_channel.dart';

class HubException implements Exception {
  const HubException(this.message);

  final String message;

  @override
  String toString() => message;
}

enum HubState { disconnected, connecting, connected, reconnecting }

/// Minimal ASP.NET Core SignalR client (JSON protocol over WebSockets, no negotiate step).
/// Works on Android, iOS and web. Reconnects automatically like the official JS client.
class HubClient {
  HubClient({required this.url, required this.accessToken});

  /// e.g. https://server/hubs/room
  final Uri url;
  final String Function() accessToken;

  void Function()? onReconnecting;
  Future<void> Function()? onReconnected;
  void Function()? onClosed;

  HubState state = HubState.disconnected;

  static const _separator = '\u001e';
  static const _reconnectDelays = [0, 2, 5, 10, 20, 30, 30, 30];

  final _handlers = <String, void Function(List<dynamic> args)>{};
  final _pending = <String, Completer<dynamic>>{};
  WebSocketChannel? _channel;
  StreamSubscription<dynamic>? _subscription;
  Timer? _ping;
  var _nextId = 0;
  var _stopped = false;
  var _reconnecting = false;

  void on(String method, void Function(List<dynamic> args) handler) => _handlers[method.toLowerCase()] = handler;

  Future<void> start() async {
    _stopped = false;
    state = HubState.connecting;
    await _connect();
  }

  Future<void> stop() async {
    _stopped = true;
    state = HubState.disconnected;
    await _closeSocket();
  }

  Future<dynamic> invoke(String method, [List<Object?> args = const []]) {
    final channel = _channel;
    if (channel == null || state != HubState.connected) {
      return Future.error(const HubException('Sem conexão com a sala.'));
    }
    final id = '${_nextId++}';
    final completer = Completer<dynamic>();
    _pending[id] = completer;
    _send({'type': 1, 'invocationId': id, 'target': method, 'arguments': args});
    return completer.future.timeout(const Duration(seconds: 30), onTimeout: () {
      _pending.remove(id);
      throw const HubException('O servidor demorou para responder.');
    });
  }

  Future<void> _connect() async {
    final wsUrl = url.replace(
      scheme: url.scheme == 'https' ? 'wss' : 'ws',
      queryParameters: {...url.queryParameters, 'access_token': accessToken()},
    );
    final channel = WebSocketChannel.connect(wsUrl);
    await channel.ready.timeout(const Duration(seconds: 15));
    _channel = channel;

    final handshake = Completer<void>();
    var buffer = '';
    _subscription = channel.stream.listen(
      (data) {
        buffer += data is String ? data : utf8.decode(data as List<int>);
        var index = buffer.indexOf(_separator);
        while (index >= 0) {
          final frame = buffer.substring(0, index);
          buffer = buffer.substring(index + 1);
          index = buffer.indexOf(_separator);

          final message = jsonDecode(frame) as Map<String, dynamic>;
          if (!handshake.isCompleted) {
            final error = message['error'];
            error == null ? handshake.complete() : handshake.completeError(HubException(error as String));
            continue;
          }
          _onMessage(message);
        }
      },
      onDone: () {
        if (!handshake.isCompleted) handshake.completeError(const HubException('Conexão recusada pelo servidor.'));
        _onDisconnected();
      },
      onError: (_) {},
    );

    channel.sink.add('${jsonEncode({'protocol': 'json', 'version': 1})}$_separator');
    await handshake.future.timeout(const Duration(seconds: 15));
    state = HubState.connected;
    _ping = Timer.periodic(const Duration(seconds: 15), (_) => _send({'type': 6}));
  }

  void _onMessage(Map<String, dynamic> message) {
    switch (message['type']) {
      case 1: // invocation from the server
        final handler = _handlers[(message['target'] as String).toLowerCase()];
        handler?.call(List<dynamic>.from(message['arguments'] as List? ?? const []));
      case 3: // completion of one of our invocations
        final completer = _pending.remove(message['invocationId']);
        final error = message['error'] as String?;
        if (error != null) {
          completer?.completeError(HubException(_cleanError(error)));
        } else {
          completer?.complete(message['result']);
        }
      case 7: // server is closing the connection
        _channel?.sink.close();
    }
  }

  Future<void> _onDisconnected() async {
    _ping?.cancel();
    _channel = null;
    for (final completer in _pending.values) {
      completer.completeError(const HubException('Conexão perdida.'));
    }
    _pending.clear();

    // A failed attempt inside the loop below also lands here; the loop handles it.
    if (_stopped || _reconnecting || state == HubState.connecting) return;

    _reconnecting = true;
    state = HubState.reconnecting;
    onReconnecting?.call();
    try {
      for (final delay in _reconnectDelays) {
        await Future<void>.delayed(Duration(seconds: delay));
        if (_stopped) return;
        try {
          await _connect();
          _reconnecting = false;
          await onReconnected?.call();
          return;
        } catch (_) {
          await _closeSocket();
          state = HubState.reconnecting;
        }
      }
      state = HubState.disconnected;
      onClosed?.call();
    } finally {
      _reconnecting = false;
    }
  }

  void _send(Map<String, dynamic> message) => _channel?.sink.add('${jsonEncode(message)}$_separator');

  Future<void> _closeSocket() async {
    _ping?.cancel();
    final subscription = _subscription;
    final channel = _channel;
    _subscription = null;
    _channel = null;
    await subscription?.cancel();
    await channel?.sink.close();
  }

  /// "An unexpected error occurred invoking 'X' on the server. HubException: Sala não encontrada." -> "Sala não encontrada."
  static String _cleanError(String error) {
    const marker = 'HubException: ';
    final index = error.indexOf(marker);
    return index >= 0 ? error.substring(index + marker.length) : error;
  }
}
