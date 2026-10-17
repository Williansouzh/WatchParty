import { api } from '../api.js';
import { esc, icon } from '../ui.js';

export async function render(root, { mode, onAuthenticated }) {
  const isRegister = mode === 'register';
  root.innerHTML = `
    <div class="auth-wrap">
      <div class="auth-card">
        <div class="brand" style="justify-content:center"><span class="logo">${icon('play')}</span>WatchParty</div>
        <h1>${isRegister ? 'Crie sua conta para assistir com a galera' : 'Entrar no WatchParty'}</h1>
        <form novalidate>
          <div class="form-error hidden" data-error></div>
          ${isRegister ? `
            <div class="field"><label for="displayName">Seu nome</label>
              <input class="input" id="displayName" name="displayName" maxlength="50" placeholder="Como seus amigos te chamam" required></div>` : ''}
          <div class="field"><label for="userName">Usuário</label>
            <input class="input" id="userName" name="userName" maxlength="32" autocomplete="username" autocapitalize="none" placeholder="usuario" required></div>
          <div class="field"><label for="password">Senha</label>
            <input class="input" id="password" name="password" type="password" autocomplete="${isRegister ? 'new-password' : 'current-password'}" placeholder="Senha" required></div>
          ${isRegister ? `
            <div class="field"><label for="inviteCode">Código de convite</label>
              <input class="input" id="inviteCode" name="inviteCode" autocomplete="off" placeholder="Peça para quem criou o servidor" required></div>` : ''}
          <button class="btn btn-primary" type="submit">${isRegister ? 'Criar conta' : 'Entrar'}</button>
        </form>
        <p class="alt">${isRegister
          ? 'Já tem uma conta? <a href="#/login">Entrar</a>'
          : 'Não tem conta? <a href="#/register">Cadastre-se com um convite</a>'}</p>
      </div>
    </div>`;

  const form = root.querySelector('form');
  const error = root.querySelector('[data-error]');
  const button = form.querySelector('button');
  form.querySelector('input').focus();

  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    error.classList.add('hidden');
    button.disabled = true;
    const data = Object.fromEntries(new FormData(form));
    try {
      const user = isRegister ? await api.register(data) : await api.login(data.userName, data.password);
      onAuthenticated(user);
    } catch (err) {
      error.innerHTML = esc(err.message);
      error.classList.remove('hidden');
    } finally {
      button.disabled = false;
    }
  });
}
