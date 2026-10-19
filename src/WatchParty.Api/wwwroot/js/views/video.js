import { api } from '../api.js';
import {
  addToPlaylistMenu, colorFor, confirmDialog, esc, formDialog, formatBytes, formatDuration, icon,
  setHero, startRoomForVideo, thumb, timeAgo, toast,
} from '../ui.js';

export async function render(root, ctx) {
  let video = await api.videos.get(ctx.params.id);
  if (!ctx.isCurrent()) return;
  let poll;

  const draw = () => {
    setHero(colorFor(video.id));
    const isOwner = video.uploadedById === ctx.user.id;
    const ready = video.status === 'Ready';
    root.innerHTML = `
      <div class="hero">
        ${thumb(video, 'hero-cover wide')}
        <div class="hero-meta">
          <div class="hero-kind">Vídeo</div>
          <h1 class="hero-title small">${esc(video.title)}</h1>
          <div class="hero-sub">
            <b>${esc(video.uploadedByName)}</b>
            <span class="dot">${timeAgo(video.uploadedAt)}</span>
            ${video.durationSeconds ? `<span class="dot">${formatDuration(video.durationSeconds)}</span>` : ''}
            ${video.sizeBytes ? `<span class="dot">${formatBytes(video.sizeBytes)}</span>` : ''}
          </div>
        </div>
      </div>
      <div class="action-row">
        ${ready ? `<button class="play-fab" data-watch title="Assistir em grupo">${icon('play')}</button>` : ''}
        <button class="icon-btn big ${video.isFavorite ? 'on' : ''}" data-fav title="${video.isFavorite ? 'Remover dos curtidos' : 'Curtir'}">${icon(video.isFavorite ? 'heart' : 'heartOutline')}</button>
        <button class="icon-btn big" data-playlist title="Adicionar à playlist">${icon('playlistAdd')}</button>
        ${isOwner ? `<button class="icon-btn big" data-more title="Mais opções">${icon('more')}</button>` : ''}
      </div>
      <div class="content-pad">
        ${video.status === 'Processing' ? `
          <div class="processing-banner"><span class="spinner"></span>
            <div style="flex:1"><b>Preparando o vídeo…${video.processingProgress != null ? ` ${Math.round(video.processingProgress * 100)}%` : ''}</b>
              <div class="description">Estamos convertendo para funcionar em qualquer celular ou navegador. Filmes grandes podem levar alguns minutos.</div>
              ${video.processingProgress != null ? `<div class="progress-inline"><div style="width:${(video.processingProgress * 100).toFixed(1)}%"></div></div>` : ''}
            </div>
          </div>` : ''}
        ${video.status === 'Failed' ? `
          <div class="processing-banner" style="background:#4a1a1f"><div><b>Não foi possível converter esse vídeo.</b>
            <div class="description">${esc(video.failureReason ?? '')} Tente outro arquivo.</div></div></div>` : ''}
        ${ready ? `<p class="description" style="margin-top:0">Toque no <b style="color:var(--accent)">play</b> para criar uma sala e mandar o link pra galera.</p>` : ''}
        ${video.description ? `<h2 style="font-size:20px">Sobre</h2><p class="description">${esc(video.description)}</p>` : ''}
        ${ready ? `
          <h2 style="font-size:20px">Legendas</h2>
          <div class="subtitle-list">
            ${video.subtitles.length ? video.subtitles.map((s) => `
              <div class="subtitle-row">${icon('chat')}<span>${esc(s.label)}${s.language ? ` <small style="color:var(--text-sub)">(${esc(s.language)})</small>` : ''}</span>
                ${s.uploadedById === ctx.user.id || isOwner ? `<button class="icon-btn" data-del-sub="${s.id}" title="Apagar legenda">${icon('trash')}</button>` : ''}
              </div>`).join('') : '<p class="description" style="margin:0">Nenhuma legenda ainda. Legendas que vêm dentro do arquivo (MKV) aparecem aqui sozinhas.</p>'}
          </div>
          <form class="subtitle-row" data-sub-form style="max-width:520px;flex-wrap:wrap">
            <input class="input" name="label" placeholder="Nome (ex: Português)" maxlength="60" required style="height:38px;flex:1;min-width:140px">
            <select class="input" name="language" style="height:38px;width:auto"><option value="pt">pt</option><option value="en">en</option><option value="es">es</option><option value="">outro</option></select>
            <label class="btn btn-sm btn-outline" style="cursor:pointer">${icon('upload')}Enviar .srt/.vtt<input type="file" accept=".srt,.vtt" hidden name="file"></label>
          </form>
          ${video.hasHls ? '<p class="description" style="font-size:13px">✓ Disponível em várias qualidades: o player se ajusta à internet de cada um.</p>' : ''}` : ''}
      </div>`;

    root.querySelector('[data-watch]')?.addEventListener('click', () => startRoomForVideo(video.id, video.title));
    root.querySelectorAll('[data-del-sub]').forEach((b) => b.addEventListener('click', async () => {
      try {
        await api.videos.removeSubtitle(video.id, b.dataset.delSub);
        video = await api.videos.get(video.id);
        draw();
      } catch (err) { toast(err.message, 'error'); }
    }));
    const subForm = root.querySelector('[data-sub-form]');
    subForm?.querySelector('input[type=file]').addEventListener('change', async (e) => {
      const file = e.target.files[0];
      const label = subForm.label.value.trim() || file?.name.replace(/\.[^.]+$/, '');
      if (!file) return;
      try {
        await api.videos.uploadSubtitle(video.id, file, label, subForm.language.value);
        toast('Legenda adicionada', 'ok');
        video = await api.videos.get(video.id);
        draw();
      } catch (err) { toast(err.message, 'error'); }
    });
    root.querySelector('[data-fav]').addEventListener('click', async () => {
      try {
        const { isFavorite } = await api.videos.toggleFavorite(video.id);
        video = { ...video, isFavorite };
        toast(isFavorite ? 'Adicionado aos curtidos' : 'Removido dos curtidos', 'ok');
        draw();
      } catch (err) { toast(err.message, 'error'); }
    });
    root.querySelector('[data-playlist]').addEventListener('click', (e) => addToPlaylistMenu(e.currentTarget, video.id));
    root.querySelector('[data-more]')?.addEventListener('click', (e) => {
      import('../ui.js').then(({ openMenu }) => openMenu(e.currentTarget, [
        { label: 'Editar detalhes', icon: 'edit', onClick: edit },
        { label: 'Excluir vídeo', icon: 'trash', onClick: remove },
      ]));
    });
  };

  const edit = async () => {
    const values = await formDialog('Editar detalhes', [
      { name: 'title', label: 'Título', value: video.title, required: true, max: 200 },
      { name: 'description', label: 'Descrição', value: video.description, type: 'textarea', max: 2000 },
    ]);
    if (!values) return;
    try {
      await api.videos.update(video.id, values);
      video = { ...video, ...values };
      draw();
    } catch (err) { toast(err.message, 'error'); }
  };

  const remove = async () => {
    if (!await confirmDialog('Excluir vídeo?', `"${video.title}" será apagado para todo mundo.`)) return;
    try {
      await api.videos.remove(video.id);
      toast('Vídeo excluído', 'ok');
      window.dispatchEvent(new Event('library:changed'));
      location.hash = '#/library';
    } catch (err) { toast(err.message, 'error'); }
  };

  draw();

  // Refresh while the server converts the file.
  if (video.status === 'Processing') {
    poll = setInterval(async () => {
      try {
        const fresh = await api.videos.get(video.id);
        if (!ctx.isCurrent()) return;
        video = fresh;
        draw();
        if (fresh.status !== 'Processing') {
          clearInterval(poll);
          if (fresh.status === 'Ready') toast('Vídeo pronto para assistir!', 'ok');
        }
      } catch { clearInterval(poll); }
    }, 2500);
  }

  return () => clearInterval(poll);
}
