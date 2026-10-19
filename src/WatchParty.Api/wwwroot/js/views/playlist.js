import { api } from '../api.js';
import {
  colorFor, confirmDialog, esc, formDialog, formatDuration, icon, openMenu, pickVideo,
  playlistCover, setHero, toast,
} from '../ui.js';
import { bindTrackTable, trackTable } from './tracks.js';

export async function render(root, ctx) {
  const id = ctx.params.id;

  const draw = async () => {
    const playlist = await api.playlists.get(id);
    if (!ctx.isCurrent()) return;
    setHero(colorFor(playlist.id));

    const isOwner = playlist.ownerId === ctx.user.id;
    const total = playlist.items.reduce((sum, i) => sum + (i.video.durationSeconds ?? 0), 0);
    const cover = playlist.items.find((i) => i.video.hasThumbnail)?.video.id;
    const firstReady = playlist.items.find((i) => i.video.status === 'Ready')?.video;

    root.innerHTML = `
      <div class="hero">
        ${playlistCover({ id: playlist.id, coverVideoId: cover }, 'hero-cover')}
        <div class="hero-meta">
          <div class="hero-kind">Playlist</div>
          <h1 class="hero-title">${esc(playlist.name)}</h1>
          ${playlist.description ? `<p class="description" style="color:rgba(255,255,255,.7);margin:0 0 8px">${esc(playlist.description)}</p>` : ''}
          <div class="hero-sub">
            <b>${esc(playlist.ownerName)}</b>
            <span class="dot">${playlist.items.length} vídeo${playlist.items.length === 1 ? '' : 's'}${total ? `, ${formatDuration(total)}` : ''}</span>
          </div>
        </div>
      </div>
      <div class="action-row">
        ${firstReady ? `<button class="play-fab" data-play title="Assistir em grupo">${icon('play')}</button>` : ''}
        <button class="btn btn-sm btn-outline" data-add>${icon('add')}Adicionar vídeos</button>
        ${isOwner ? `<button class="icon-btn big" data-more title="Mais opções">${icon('more')}</button>` : ''}
      </div>
      <div class="content-pad">
        ${playlist.items.length
          ? trackTable(playlist.items.map((i) => ({ video: i.video, addedAt: i.addedAt })), { showRemove: true })
          : '<div class="empty-state"><h3>Vamos encher essa playlist</h3><p>Qualquer amigo pode adicionar vídeos aqui.</p></div>'}
      </div>`;

    // Starts a "maratona": first video now, the rest queued in the room.
    root.querySelector('[data-play]')?.addEventListener('click', async () => {
      try {
        const { code } = await api.rooms.create({ name: `Maratona: ${playlist.name}`.slice(0, 80), playlistId: playlist.id });
        location.hash = `#/room/${code}`;
      } catch (err) { toast(err.message, 'error'); }
    });
    root.querySelector('[data-add]').addEventListener('click', async () => {
      const video = await pickVideo('Adicionar à playlist');
      if (!video) return;
      try {
        await api.playlists.addVideo(id, video.id);
        window.dispatchEvent(new Event('library:changed'));
        await draw();
      } catch (err) { toast(err.message, 'error'); }
    });
    root.querySelector('[data-more]')?.addEventListener('click', (e) => openMenu(e.currentTarget, [
      {
        label: 'Editar detalhes', icon: 'edit', onClick: async () => {
          const values = await formDialog('Editar playlist', [
            { name: 'name', label: 'Nome', value: playlist.name, required: true, max: 100 },
            { name: 'description', label: 'Descrição', value: playlist.description, type: 'textarea', max: 500 },
          ]);
          if (!values) return;
          try {
            await api.playlists.update(id, values);
            window.dispatchEvent(new Event('library:changed'));
            await draw();
          } catch (err) { toast(err.message, 'error'); }
        },
      },
      {
        label: 'Excluir playlist', icon: 'trash', onClick: async () => {
          if (!await confirmDialog('Excluir playlist?', `"${playlist.name}" será apagada. Os vídeos continuam na biblioteca.`)) return;
          try {
            await api.playlists.remove(id);
            window.dispatchEvent(new Event('library:changed'));
            location.hash = '#/library?f=playlists';
          } catch (err) { toast(err.message, 'error'); }
        },
      },
    ]));

    bindTrackTable(root, {
      onRemove: async (videoId) => {
        try {
          await api.playlists.removeVideo(id, videoId);
          window.dispatchEvent(new Event('library:changed'));
          await draw();
        } catch (err) { toast(err.message, 'error'); }
      },
      onUnlike: () => {},
    });
  };

  await draw();
}
