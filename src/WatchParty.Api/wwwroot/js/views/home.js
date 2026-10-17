import { api } from '../api.js';
import { bindCards, esc, greeting, icon, playlistCover, roomCard, videoCard } from '../ui.js';

export async function render(root, { user, isCurrent }) {
  root.innerHTML = `<div class="page greeting-bg"><h1 class="page-title">${greeting()}, ${esc(user.displayName.split(' ')[0])}</h1><div class="spinner"></div></div>`;

  const [videos, playlists, rooms] = await Promise.all([api.videos.list({ take: 18 }), api.playlists.list(), api.rooms.list()]);
  if (!isCurrent()) return;

  const liveRooms = rooms.filter((r) => r.onlineCount > 0);
  const shownRooms = (liveRooms.length ? liveRooms : rooms).slice(0, 6);

  root.innerHTML = `
    <div class="page greeting-bg">
      <h1 class="page-title">${greeting()}, ${esc(user.displayName.split(' ')[0])}</h1>

      <div class="shortcuts">
        <a class="shortcut" href="#/liked"><div class="cover cover-liked">${icon('heart')}</div><span>Vídeos curtidos</span></a>
        ${playlists.slice(0, 5).map((p) => `<a class="shortcut" href="#/playlist/${p.id}">${playlistCover(p)}<span>${esc(p.name)}</span></a>`).join('')}
        ${playlists.length < 5 ? `<a class="shortcut" href="#/rooms"><div class="cover placeholder" style="--c1:#1e3264;--c2:#1ed760">${icon('live')}</div><span>Salas</span></a>` : ''}
      </div>

      ${shownRooms.length ? `
        <section class="section">
          <div class="section-head"><h2>${liveRooms.length ? 'Rolando agora' : 'Suas salas'}</h2><a href="#/rooms">Mostrar tudo</a></div>
          <div class="grid">${shownRooms.map(roomCard).join('')}</div>
        </section>` : ''}

      <section class="section">
        <div class="section-head"><h2>Adicionados recentemente</h2>${videos.length ? '<a href="#/library">Mostrar tudo</a>' : ''}</div>
        ${videos.length
          ? `<div class="grid">${videos.map(videoCard).join('')}</div>`
          : `<div class="empty-state"><h3>Sua biblioteca está vazia</h3><p>Envie o primeiro filme e chame a galera.</p>
             <a class="btn btn-primary" href="#/upload" style="margin-top:16px">${icon('upload')}Enviar vídeo</a></div>`}
      </section>
    </div>`;

  bindCards(root);
}
