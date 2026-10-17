import { api } from '../api.js';
import { createPlaylist } from '../app.js';
import { bindCards, esc, icon, playlistCover, setHero, startRoomForVideo, toast, videoCard } from '../ui.js';
import { trackTable, bindTrackTable } from './tracks.js';

const FILTERS = [
  { key: 'all', label: 'Tudo' },
  { key: 'favorites', label: 'Curtidos' },
  { key: 'mine', label: 'Meus envios' },
  { key: 'playlists', label: 'Playlists' },
];

export async function render(root, { query, isCurrent }) {
  const active = FILTERS.some((f) => f.key === query.get('f')) ? query.get('f') : 'all';
  root.innerHTML = `
    <div class="page">
      <h1 class="page-title">Sua Biblioteca</h1>
      <div class="chips" style="margin-bottom:24px">
        ${FILTERS.map((f) => `<a class="chip ${f.key === active ? 'active' : ''}" href="#/library?f=${f.key}">${f.label}</a>`).join('')}
      </div>
      <div data-content><div class="spinner"></div></div>
    </div>`;

  const content = root.querySelector('[data-content]');
  bindCards(content);

  if (active === 'playlists') {
    const playlists = await api.playlists.list();
    if (!isCurrent()) return;
    content.innerHTML = `
      <div class="grid">
        <article class="card" data-new><div class="card-media"><div class="thumb placeholder" style="--c1:#2a2a2a;--c2:#121212">${icon('add')}</div></div>
          <div class="card-title">Criar playlist</div><div class="card-sub">Junte filmes pra maratonar</div></article>
        ${playlists.map((p) => `
          <article class="card" data-href="#/playlist/${p.id}">
            <div class="card-media">${playlistCover(p, 'thumb')}</div>
            <div class="card-title">${esc(p.name)}</div>
            <div class="card-sub">Playlist • ${p.itemCount} vídeo${p.itemCount === 1 ? '' : 's'} • ${esc(p.ownerName)}</div>
          </article>`).join('')}
      </div>`;
    content.querySelector('[data-new]').onclick = createPlaylist;
    return;
  }

  const videos = await api.videos.list({ filter: active });
  if (!isCurrent()) return;
  content.innerHTML = videos.length
    ? `<div class="grid">${videos.map(videoCard).join('')}</div>`
    : `<div class="empty-state"><h3>${active === 'favorites' ? 'Nenhum vídeo curtido' : 'Nada por aqui ainda'}</h3>
       <p>${active === 'favorites' ? 'Toque no coração de um vídeo para salvar aqui.' : 'Envie um vídeo para começar.'}</p></div>`;
}

/** "Vídeos curtidos" page, styled like Spotify's Liked Songs. */
export const liked = {
  async render(root, { user, isCurrent }) {
    setHero('#5038a0');
    const videos = await api.videos.list({ filter: 'favorites' });
    if (!isCurrent()) return;

    root.innerHTML = `
      <div class="hero">
        <div class="hero-cover cover-liked">${icon('heart').replace('class="icon ', 'style="width:40%;height:40%" class="icon ')}</div>
        <div class="hero-meta">
          <div class="hero-kind">Playlist</div>
          <h1 class="hero-title">Vídeos curtidos</h1>
          <div class="hero-sub"><b>${esc(user.displayName)}</b><span class="dot">${videos.length} vídeo${videos.length === 1 ? '' : 's'}</span></div>
        </div>
      </div>
      <div class="action-row">
        ${videos.some((v) => v.status === 'Ready') ? `<button class="play-fab" data-play title="Assistir em grupo">${icon('play')}</button>` : ''}
      </div>
      <div class="content-pad">
        ${videos.length ? trackTable(videos.map((v) => ({ video: v, addedAt: null })), { showRemove: false })
          : '<div class="empty-state"><h3>Os vídeos que você curtir aparecem aqui</h3><p>Salve vídeos tocando no ícone de coração.</p></div>'}
      </div>`;

    root.querySelector('[data-play]')?.addEventListener('click', () => {
      const first = videos.find((v) => v.status === 'Ready');
      startRoomForVideo(first.id, first.title);
    });
    bindTrackTable(root, {
      onRemove: async () => {},
      onUnlike: async (videoId) => {
        try {
          await api.videos.toggleFavorite(videoId);
          root.querySelector(`tr[data-video="${videoId}"]`)?.remove();
          toast('Removido dos curtidos', 'ok');
        } catch (err) { toast(err.message, 'error'); }
      },
    });
  },
};
