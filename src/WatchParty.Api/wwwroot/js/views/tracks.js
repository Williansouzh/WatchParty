// Spotify-style track list used by playlists and liked videos.
import { esc, formatClock, icon, thumb, timeAgo } from '../ui.js';

/** items: [{ video, addedAt }] */
export function trackTable(items, { showRemove }) {
  return `
    <table class="tracks">
      <thead><tr>
        <th class="num">#</th><th>Título</th><th class="hide-sm">Enviado por</th>
        <th class="hide-sm">${items.some((i) => i.addedAt) ? 'Adicionado' : ''}</th><th style="text-align:right">⏱</th><th></th>
      </tr></thead>
      <tbody>
        ${items.map(({ video, addedAt }, i) => `
          <tr data-video="${video.id}">
            <td class="num">${i + 1}</td>
            <td><div class="t-main">${thumb(video)}<div style="min-width:0"><div class="t-title">${esc(video.title)}</div>
              <div>${video.status === 'Ready' ? '' : video.status === 'Processing' ? 'Processando…' : 'Falhou'}</div></div></div></td>
            <td class="hide-sm">${esc(video.uploadedByName)}</td>
            <td class="hide-sm">${addedAt ? timeAgo(addedAt) : ''}</td>
            <td style="text-align:right;font-variant-numeric:tabular-nums">${video.durationSeconds ? formatClock(video.durationSeconds) : '–'}</td>
            <td class="row-actions" style="width:48px">
              ${showRemove
                ? `<button class="icon-btn" data-remove="${video.id}" title="Remover da playlist">${icon('close')}</button>`
                : `<button class="icon-btn on" data-unlike="${video.id}" title="Descurtir">${icon('heart')}</button>`}
            </td>
          </tr>`).join('')}
      </tbody>
    </table>`;
}

export function bindTrackTable(root, { onRemove, onUnlike }) {
  root.querySelector('.tracks')?.addEventListener('click', (e) => {
    const remove = e.target.closest('[data-remove]');
    if (remove) { e.stopPropagation(); onRemove(remove.dataset.remove); return; }
    const unlike = e.target.closest('[data-unlike]');
    if (unlike) { e.stopPropagation(); onUnlike(unlike.dataset.unlike); return; }
    const row = e.target.closest('[data-video]');
    if (row) location.hash = `#/video/${row.dataset.video}`;
  });
}
