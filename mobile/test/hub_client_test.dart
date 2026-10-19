// Talks to a real WatchParty server. Run with:
//   WATCHPARTY_SERVER=http://localhost:5080 WATCHPARTY_USER=will WATCHPARTY_PASSWORD=senha123 flutter test test/hub_client_test.dart
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:watchparty/core/hub_client.dart';

void main() {
  final server = Platform.environment['WATCHPARTY_SERVER'];
  final skip = server == null ? 'set WATCHPARTY_SERVER to run against a live server' : null;

  Future<String> login() async {
    final client = HttpClient();
    final request = await client.postUrl(Uri.parse('$server/api/auth/token/login'));
    request.headers.contentType = ContentType.json;
    request.write(jsonEncode({
      'userName': Platform.environment['WATCHPARTY_USER'] ?? 'will',
      'password': Platform.environment['WATCHPARTY_PASSWORD'] ?? 'senha123',
    }));
    final response = await request.close();
    final body = jsonDecode(await response.transform(utf8.decoder).join()) as Map<String, dynamic>;
    client.close();
    return body['accessToken'] as String;
  }

  test('connects, invokes and reports hub errors in plain text', () async {
    final token = await login();
    final hub = HubClient(url: Uri.parse('$server/hubs/room'), accessToken: () => token);
    await hub.start();
    expect(hub.state, HubState.connected);

    final serverTime = await hub.invoke('GetServerTime') as num;
    expect((serverTime - DateTime.now().millisecondsSinceEpoch).abs(), lessThan(60000));

    await expectLater(
      hub.invoke('JoinRoom', ['XXXXXX']),
      throwsA(isA<HubException>().having((e) => e.message, 'message', 'Sala não encontrada.')),
    );
    await expectLater(hub.invoke('GetPlayback'), throwsA(isA<HubException>()));

    await hub.stop();
    expect(hub.state, HubState.disconnected);
  }, skip: skip);

  test('rejects a bad token', () async {
    final hub = HubClient(url: Uri.parse('$server/hubs/room'), accessToken: () => 'invalid');
    await expectLater(hub.start(), throwsA(anything));
  }, skip: skip);
}
