import { api } from '../api.js';
import { bindCards, formDialog, icon, pickVideo, roomCard, toast } from '../ui.js';

export async function render(root, { isCurrent }) {
  const rooms = await api.rooms.list();
  if (!isCurrent()) return;

  root.innerHTML = `
    <div class="page">
      <h1 class="page-title">Salas</h1>
      <div style="display:flex;gap:12px;flex-wrap:wrap;margin-bottom:32px">
        <button class="btn btn-primary" data-create>${icon('add')}Criar sala</button>
        <form data-join style="display:flex;gap:8px">
          <input class="input" name="code" placeholder="Código (ex: K7P2QX)" maxlength="6" style="width:200px;text-transform:uppercase;border-radius:999px" autocomplete="off">
          <button class="btn btn-outline">Entrar</button>
        </form>
      </div>
      ${rooms.length
        ? `<div class="grid">${rooms.map(roomCard).join('')}</div>`
        : '<div class="empty-state"><h3>Nenhuma sala ainda</h3><p>Crie uma sala, escolha um filme e mande o link pros amigos.</p></div>'}
    </div>`;

  bindCards(root);

  root.querySelector('[data-join]').addEventListener('submit', (e) => {
    e.preventDefault();
    const code = new FormData(e.target).get('code').trim().toUpperCase();
    if (code) location.hash = `#/room/${code}`;
  });

  root.querySelector('[data-create]').addEventListener('click', async () => {
    const values = await formDialog('Criar sala', [{ name: 'name', label: 'Nome da sala', value: 'Sessão de cinema', required: true, max: 80 }], 'Escolher vídeo');
    if (!values) return;
    const video = await pickVideo('O que vamos assistir?');
    try {
      const { code } = await api.rooms.create({ name: values.name, videoId: video?.id ?? null });
      location.hash = `#/room/${code}`;
    } catch (err) { toast(err.message, 'error'); }
  });
}
