import 'package:flutter/material.dart';

import '../../core/api.dart';
import '../../core/app_scope.dart';
import '../../core/theme.dart';

class LoginPage extends StatefulWidget {
  const LoginPage({super.key});

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final _form = GlobalKey<FormState>();
  final _server = TextEditingController();
  final _displayName = TextEditingController();
  final _userName = TextEditingController();
  final _password = TextEditingController();
  final _invite = TextEditingController();
  var _register = false;
  var _busy = false;
  var _editServer = false;
  String? _error;

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_server.text.isEmpty) {
      _server.text = context.session.serverUrl ?? '';
      _editServer = _server.text.isEmpty;
    }
  }

  @override
  void dispose() {
    for (final c in [_server, _displayName, _userName, _password, _invite]) {
      c.dispose();
    }
    super.dispose();
  }

  Future<void> _submit() async {
    if (!_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });

    final session = context.session;
    final api = context.api;
    try {
      await session.setServer(_server.text);
      final token = _register
          ? await api.register(
              userName: _userName.text.trim(),
              displayName: _displayName.text.trim(),
              password: _password.text,
              inviteCode: _invite.text.trim(),
            )
          : await api.login(_userName.text.trim(), _password.text);
      final user = await api.me(token: token);
      await session.signIn(token, user);
    } on ApiException catch (e) {
      setState(() => _error = e.message);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  String? _required(String? v) => v == null || v.trim().isEmpty ? 'Obrigatório' : null;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      body: DecoratedBox(
        decoration: const BoxDecoration(
          gradient: LinearGradient(begin: Alignment.topCenter, end: Alignment.bottomCenter, colors: [Color(0xFF2C4A36), AppColors.background]),
        ),
        child: SafeArea(
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 400),
                child: Form(
                  key: _form,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      const _Logo(),
                      const SizedBox(height: 24),
                      Text(
                        _register ? 'Crie sua conta para assistir com a galera' : 'Entrar no WatchParty',
                        textAlign: TextAlign.center,
                        style: Theme.of(context).textTheme.headlineLarge,
                      ),
                      const SizedBox(height: 28),
                      if (_error != null)
                        Container(
                          key: const Key('login-error'),
                          margin: const EdgeInsets.only(bottom: 16),
                          padding: const EdgeInsets.all(12),
                          decoration: BoxDecoration(color: const Color(0xFFE9142A), borderRadius: BorderRadius.circular(4)),
                          child: Text(_error!, style: const TextStyle(fontWeight: FontWeight.w600)),
                        ),
                      if (_editServer)
                        _field(_server, 'Endereço do servidor', hint: 'https://seu-servidor.com',
                            keyboard: TextInputType.url, validator: _required)
                      else
                        Padding(
                          padding: const EdgeInsets.only(bottom: 16),
                          child: Row(
                            children: [
                              const Icon(Icons.dns_rounded, size: 18, color: AppColors.textSub),
                              const SizedBox(width: 8),
                              Expanded(
                                child: Text(_server.text,
                                    overflow: TextOverflow.ellipsis, style: const TextStyle(color: AppColors.textSub)),
                              ),
                              TextButton(onPressed: () => setState(() => _editServer = true), child: const Text('Alterar')),
                            ],
                          ),
                        ),
                      if (_register) _field(_displayName, 'Seu nome', hint: 'Como seus amigos te chamam', validator: _required),
                      _field(_userName, 'Usuário', hint: 'usuario', validator: _required, autofill: AutofillHints.username),
                      _field(_password, 'Senha', obscure: true, validator: _required, autofill: AutofillHints.password),
                      if (_register) _field(_invite, 'Código de convite', hint: 'Peça para quem criou o servidor', validator: _required),
                      const SizedBox(height: 8),
                      FilledButton(
                        key: const Key('login-submit'),
                        onPressed: _busy ? null : _submit,
                        child: _busy
                            ? const SizedBox(width: 22, height: 22, child: CircularProgressIndicator(strokeWidth: 3, color: Colors.black))
                            : Text(_register ? 'Criar conta' : 'Entrar'),
                      ),
                      const SizedBox(height: 24),
                      TextButton(
                        onPressed: () => setState(() {
                          _register = !_register;
                          _error = null;
                        }),
                        child: Text(
                          _register ? 'Já tem conta? Entrar' : 'Não tem conta? Cadastre-se com um convite',
                          style: const TextStyle(color: AppColors.text, fontWeight: FontWeight.w700, decoration: TextDecoration.underline),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _field(
    TextEditingController controller,
    String label, {
    String? hint,
    bool obscure = false,
    TextInputType? keyboard,
    String? Function(String?)? validator,
    String? autofill,
  }) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 16),
      child: TextFormField(
        controller: controller,
        obscureText: obscure,
        keyboardType: keyboard,
        autocorrect: false,
        validator: validator,
        autofillHints: autofill == null ? null : [autofill],
        textInputAction: TextInputAction.next,
        onFieldSubmitted: (_) => obscure ? _submit() : null,
        decoration: InputDecoration(labelText: label, hintText: hint),
      ),
    );
  }
}

class _Logo extends StatelessWidget {
  const _Logo();

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(color: AppColors.accent, borderRadius: BorderRadius.circular(10)),
          child: const Icon(Icons.play_arrow_rounded, color: Colors.black, size: 30),
        ),
        const SizedBox(width: 10),
        const Text('WatchParty', style: TextStyle(fontSize: 24, fontWeight: FontWeight.w900, letterSpacing: -0.5)),
      ],
    );
  }
}
