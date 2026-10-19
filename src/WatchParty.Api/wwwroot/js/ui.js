// Shared UI helpers: escaping, icons, formatting, toasts, modals, menus and reusable cards.
import { api } from './api.js';

export const esc = (value) => String(value ?? '').replace(/[&<>"']/g, (c) => (
  { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const ICONS = {
  home: 'M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z',
  search: 'M15.5 14h-.79l-.28-.27A6.471 6.471 0 0 0 16 9.5 6.5 6.5 0 1 0 9.5 16c1.61 0 3.09-.59 4.23-1.57l.27.28v.79l5 4.99L20.49 19l-4.99-5zm-6 0C7.01 14 5 11.99 5 9.5S7.01 5 9.5 5 14 7.01 14 9.5 11.99 14 9.5 14z',
  library: 'M4 6H2v14c0 1.1.9 2 2 2h14v-2H4V6zm16-4H8c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm-8 12.5v-9l6 4.5-6 4.5z',
  add: 'M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z',
  heart: 'M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z',
  heartOutline: 'M16.5 3c-1.74 0-3.41.81-4.5 2.09C10.91 3.81 9.24 3 7.5 3 4.42 3 2 5.42 2 8.5c0 3.78 3.4 6.86 8.55 11.54L12 21.35l1.45-1.32C18.6 15.36 22 12.28 22 8.5 22 5.42 19.58 3 16.5 3zm-4.4 15.55l-.1.1-.1-.1C7.14 14.24 4 11.39 4 8.5 4 6.5 5.5 5 7.5 5c1.54 0 3.04.99 3.57 2.36h1.87C13.46 5.99 14.96 5 16.5 5c2 0 3.5 1.5 3.5 3.5 0 2.89-3.14 5.74-7.9 10.05z',
  play: 'M8 5v14l11-7z',
  pause: 'M6 19h4V5H6v14zm8-14v14h4V5h-4z',
  upload: 'M9 16h6v-6h4l-7-7-7 7h4zm-4 2h14v2H5z',
  mic: 'M12 14c1.66 0 2.99-1.34 2.99-3L15 5c0-1.66-1.34-3-3-3S9 3.34 9 5v6c0 1.66 1.34 3 3 3zm5.3-3c0 3-2.54 5.1-5.3 5.1S6.7 14 6.7 11H5c0 3.41 2.72 6.23 6 6.72V21h2v-3.28c3.28-.48 6-3.3 6-6.72h-1.7z',
  micOff: 'M19 11h-1.7c0 .74-.16 1.43-.43 2.05l1.23 1.23c.56-.98.9-2.09.9-3.28zm-4.02.17c0-.06.02-.11.02-.17V5c0-1.66-1.34-3-3-3S9 3.34 9 5v.18l5.98 5.99zM4.27 3L3 4.27l6.01 6.01V11c0 1.66 1.33 3 2.99 3 .22 0 .44-.03.65-.08l1.66 1.66c-.71.33-1.5.52-2.31.52-2.76 0-5.3-2.1-5.3-5.1H5c0 3.41 2.72 6.23 6 6.72V21h2v-3.28c.91-.13 1.77-.45 2.54-.9L19.73 21 21 19.73 4.27 3z',
  headset: 'M12 1c-4.97 0-9 4.03-9 9v7c0 1.66 1.34 3 3 3h3v-8H5v-2c0-3.87 3.13-7 7-7s7 3.13 7 7v2h-4v8h3c1.66 0 3-1.34 3-3v-7c0-4.97-4.03-9-9-9z',
  hangup: 'M12 9c-1.6 0-3.15.25-4.6.72v3.1c0 .39-.23.74-.56.9-.98.49-1.87 1.12-2.66 1.85-.18.18-.43.28-.7.28-.28 0-.53-.11-.71-.29L.29 13.08a.956.956 0 0 1-.29-.7c0-.28.11-.53.29-.71C3.34 8.78 7.46 7 12 7s8.66 1.78 11.71 4.67c.18.18.29.43.29.71 0 .28-.11.53-.29.71l-2.48 2.48c-.18.18-.43.29-.71.29-.27 0-.52-.11-.7-.28a11.27 11.27 0 0 0-2.67-1.85.996.996 0 0 1-.56-.9v-3.1C15.15 9.25 13.6 9 12 9z',
  send: 'M2.01 21L23 12 2.01 3 2 10l15 2-15 2z',
  trash: 'M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z',
  edit: 'M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04a.996.996 0 0 0 0-1.41l-2.34-2.34a.996.996 0 0 0-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z',
  link: 'M3.9 12c0-1.71 1.39-3.1 3.1-3.1h4V7H7c-2.76 0-5 2.24-5 5s2.24 5 5 5h4v-1.9H7c-1.71 0-3.1-1.39-3.1-3.1zM8 13h8v-2H8v2zm9-6h-4v1.9h4c1.71 0 3.1 1.39 3.1 3.1s-1.39 3.1-3.1 3.1h-4V17h4c2.76 0 5-2.24 5-5s-2.24-5-5-5z',
  group: 'M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z',
  chat: 'M20 2H4c-1.1 0-1.99.9-1.99 2L2 22l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zM6 9h12v2H6V9zm8 5H6v-2h8v2zm4-6H6V6h12v2z',
  movie: 'M18 4l2 4h-3l-2-4h-2l2 4h-3l-2-4H8l2 4H7L5 4H4c-1.1 0-1.99.9-1.99 2L2 18c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V4h-4z',
  back: 'M15.41 7.41L14 6l-6 6 6 6 1.41-1.41L10.83 12z',
  forward: 'M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z',
  logout: 'M17 7l-1.41 1.41L18.17 11H8v2h10.17l-2.58 2.58L17 17l5-5zM4 5h8V3H4c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h8v-2H4V5z',
  live: 'M21 6h-7.59l3.29-3.29L16 2l-4 4-4-4-.71.71L10.59 6H3c-1.1 0-2 .89-2 2v12c0 1.1.9 2 2 2h18c1.1 0 2-.9 2-2V8c0-1.11-.9-2-2-2zm0 14H3V8h18v12zM9 10v8l7-4z',
  more: 'M6 10c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm12 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2zm-6 0c-1.1 0-2 .9-2 2s.9 2 2 2 2-.9 2-2-.9-2-2-2z',
  close: 'M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z',
  playlistAdd: 'M14 10H2v2h12v-2zm0-4H2v2h12V6zm4 8v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zM2 16h8v-2H2v2z',
  queue: 'M15 6H3v2h12V6zm0 4H3v2h12v-2zM3 16h8v-2H3v2zM17 6v8.18c-.31-.11-.65-.18-1-.18-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3V8h3V6h-5z',
  swap: 'M6.99 11L3 15l3.99 4v-3H14v-2H6.99v-3zM21 9l-3.99-4v3H10v2h7.01v3L21 9z',
};

export const icon = (name, cls = '') =>
  `<svg class="icon ${cls}" viewBox="0 0 24 24" aria-hidden="true"><path d="${ICONS[name]}"/></svg>`;

// ---------- Formatting ----------
export function formatClock(totalSeconds) {
  if (!Number.isFinite(totalSeconds)) return '0:00';
  const s = Math.max(0, Math.floor(totalSeconds));
  const h = Math.floor(s / 3600), m = Math.floor((s % 3600) / 60), sec = s % 60;
  const mm = h ? String(m).padStart(2, '0') : m;
  return `${h ? h + ':' : ''}${mm}:${String(sec).padStart(2, '0')}`;
}

export function formatDuration(totalSeconds) {
  if (!totalSeconds) return '';
  const h = Math.floor(totalSeconds / 3600), m = Math.round((totalSeconds % 3600) / 60);
  return h ? `${h} h ${m} min` : `${Math.max(1, m)} min`;
}

export function formatBytes(bytes) {
  if (!bytes) return '';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const i = Math.min(units.length - 1, Math.floor(Math.log(bytes) / Math.log(1024)));
  return `${(bytes / 1024 ** i).toFixed(i >= 3 ? 1 : 0)} ${units[i]}`;
}

const rtf = new Intl.RelativeTimeFormat('pt-BR', { numeric: 'auto' });
export function timeAgo(date) {
  const diff = (new Date(date).getTime() - Date.now()) / 1000;
  const steps = [[60, 'second'], [3600, 'minute'], [86400, 'hour'], [604800, 'day'], [2629800, 'week'], [31557600, 'month'], [Infinity, 'year']];
  const divisors = { second: 1, minute: 60, hour: 3600, day: 86400, week: 604800, month: 2629800, year: 31557600 };
  const [, unit] = steps.find(([limit]) => Math.abs(diff) < limit);
  return rtf.format(Math.round(diff / divisors[unit]), unit);
}

export const timeOfDay = (date) => new Date(date).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });

export function greeting() {
  const h = new Date().getHours();
  return h < 5 ? 'Boa noite' : h < 12 ? 'Bom dia' : h < 18 ? 'Boa tarde' : 'Boa noite';
}

// ---------- Visual helpers ----------
const PALETTE = ['#e13300', '#1e3264', '#8d67ab', '#e8115b', '#148a08', '#bc5900', '#509bf5', '#477d95', '#af2896', '#503750', '#27856a', '#dc148c'];
export function colorFor(seed) {
  let hash = 0;
  for (const ch of String(seed)) hash = (hash * 31 + ch.charCodeAt(0)) | 0;
  return PALETTE[Math.abs(hash) % PALETTE.length];
}

export function avatar(name, color, cls = '') {
  return `<span class="avatar ${cls}" style="background:${esc(color)}" title="${esc(name)}">${esc((name || '?').trim()[0]?.toUpperCase() ?? '?')}</span>`;
}

/** Thumbnail image, or a colored placeholder when the video has none (yet). */
export function thumb(video, cls = 'thumb') {
  if (video?.hasThumbnail) return `<img class="${cls}" src="${api.videos.thumbUrl(video.id)}" alt="" loading="lazy">`;
  const c1 = colorFor(video?.id ?? 'x'), c2 = colorFor((video?.title ?? '') + 'z');
  return `<div class="${cls} placeholder" style="--c1:${c1};--c2:${c2}">${icon('movie')}</div>`;
}

export function playlistCover(playlist, cls = 'cover') {
  if (playlist.coverVideoId) return `<img class="${cls}" src="${api.videos.thumbUrl(playlist.coverVideoId)}" alt="" style="object-fit:cover">`;
  return `<div class="${cls} placeholder" style="--c1:${colorFor(playlist.id)};--c2:#121212">${icon('queue')}</div>`;
}

export function videoSubtitle(video) {
  if (video.status === 'Processing') return 'Processando…';
  if (video.status === 'Failed') return 'Falhou ao converter';
  return [formatDuration(video.durationSeconds), video.uploadedByName].filter(Boolean).join(' • ');
}

export function videoCard(video) {
  const badge = video.status === 'Processing' ? '<span class="badge">PROCESSANDO</span>'
    : video.status === 'Failed' ? '<span class="badge failed">FALHOU</span>' : '';
  const duration = video.durationSeconds ? `<span class="duration-tag">${formatClock(video.durationSeconds)}</span>` : '';
  const play = video.status === 'Ready'
    ? `<button class="play-fab" data-watch="${video.id}" title="Assistir em grupo">${icon('play')}</button>` : '';
  return `
    <article class="card" data-href="#/video/${video.id}">
      <div class="card-media">${thumb(video)}${badge}${duration}${play}</div>
      <div class="card-title">${esc(video.title)}</div>
      <div class="card-sub">${esc(videoSubtitle(video))}</div>
    </article>`;
}

export function roomCard(room) {
  const media = room.currentVideoId
    ? thumb({ id: room.currentVideoId, title: room.currentVideoTitle, hasThumbnail: room.hasThumbnail })
    : `<div class="thumb placeholder" style="--c1:${colorFor(room.code)};--c2:#121212">${icon('live')}</div>`;
  const badge = room.onlineCount > 0 ? `<span class="badge live">AO VIVO • ${room.onlineCount}</span>` : '';
  return `
    <article class="card" data-href="#/room/${esc(room.code)}">
      <div class="card-media">${media}${badge}<button class="play-fab" data-href="#/room/${esc(room.code)}" title="Entrar">${icon('play')}</button></div>
      <div class="card-title">${esc(room.name)}</div>
      <div class="card-sub">${esc(room.currentVideoTitle ?? 'Sem vídeo')} • ${esc(room.hostName)}</div>
    </article>`;
}

/**
 * Wires the click behaviour shared by all cards: [data-watch] starts a room for a video,
 * [data-href] navigates. Call once per rendered container.
 */
export function bindCards(root) {
  root.addEventListener('click', async (e) => {
    const watch = e.target.closest('[data-watch]');
    if (watch) {
      e.stopPropagation();
      await startRoomForVideo(watch.dataset.watch, watch.dataset.title);
      return;
    }
    const link = e.target.closest('[data-href]');
    if (link && !e.target.closest('a,button:not([data-href])')) location.hash = link.dataset.href;
  });
}

export async function startRoomForVideo(videoId, title) {
  try {
    const name = title ? `Sessão: ${title}`.slice(0, 80) : 'Sessão de cinema';
    const { code } = await api.rooms.create({ name, videoId });
    location.hash = `#/room/${code}`;
  } catch (err) {
    toast(err.message, 'error');
  }
}

// ---------- Toasts ----------
/** action: { label, href } adds a link (e.g. "Entrar" on a room alert). */
export function toast(message, type = '', action = null) {
  const root = document.getElementById('toast-root');
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  el.textContent = message;
  if (action) {
    const link = Object.assign(document.createElement('a'), { href: action.href, textContent: action.label, className: 'toast-action' });
    link.addEventListener('click', () => el.remove());
    el.appendChild(link);
  }
  root.appendChild(el);
  setTimeout(() => el.remove(), action ? 8000 : 3200);
}

// ---------- Modal ----------
/** Opens a modal. `body` is HTML; `onMount(el, close)` wires it. Returns close(). */
export function openModal({ title, body, onMount, wide = false }) {
  const root = document.getElementById('modal-root');
  const backdrop = document.createElement('div');
  backdrop.className = 'modal-backdrop';
  backdrop.innerHTML = `
    <div class="modal" role="dialog" aria-modal="true" style="${wide ? 'max-width:680px' : ''}">
      <div class="modal-head"><h2>${esc(title)}</h2><button class="icon-btn" data-close title="Fechar">${icon('close')}</button></div>
      <div class="modal-body">${body}</div>
    </div>`;
  const onKey = (e) => e.key === 'Escape' && close();
  const close = () => { backdrop.remove(); document.removeEventListener('keydown', onKey); };
  backdrop.addEventListener('mousedown', (e) => (e.target === backdrop || e.target.closest('[data-close]')) && close());
  document.addEventListener('keydown', onKey);
  root.appendChild(backdrop);
  onMount?.(backdrop.querySelector('.modal'), close);
  backdrop.querySelector('input,textarea')?.focus();
  return close;
}

export function confirmDialog(title, message, confirmLabel = 'Excluir') {
  return new Promise((resolve) => {
    let answered = false;
    const close = openModal({
      title,
      body: `<p class="description">${esc(message)}</p>
        <div class="modal-actions"><button class="btn btn-sm btn-outline" data-no>Cancelar</button><button class="btn btn-sm btn-primary" data-yes>${esc(confirmLabel)}</button></div>`,
      onMount: (el, closeFn) => {
        el.querySelector('[data-no]').onclick = () => { answered = true; closeFn(); resolve(false); };
        el.querySelector('[data-yes]').onclick = () => { answered = true; closeFn(); resolve(true); };
      },
    });
    // Closing with X / Esc / backdrop counts as "no".
    const observer = new MutationObserver(() => {
      if (!document.querySelector('.modal-backdrop')) { observer.disconnect(); if (!answered) resolve(false); }
    });
    observer.observe(document.getElementById('modal-root'), { childList: true });
    void close;
  });
}

/** Simple form modal. fields: [{ name, label, value, type: 'text'|'textarea', required }]. Resolves with values or null. */
export function formDialog(title, fields, submitLabel = 'Salvar') {
  return new Promise((resolve) => {
    let done = false;
    openModal({
      title,
      body: `<form>${fields.map((f) => `
          <div class="field"><label for="f-${f.name}">${esc(f.label)}</label>
          ${f.type === 'textarea'
            ? `<textarea class="input" id="f-${f.name}" name="${f.name}" maxlength="${f.max ?? 2000}">${esc(f.value ?? '')}</textarea>`
            : `<input class="input" id="f-${f.name}" name="${f.name}" type="${f.type === 'password' ? 'password' : 'text'}" value="${esc(f.value ?? '')}" maxlength="${f.max ?? 200}" ${f.required ? 'required' : ''} autocomplete="${f.type === 'password' ? 'new-password' : 'off'}">`}
          </div>`).join('')}
        <div class="modal-actions"><button type="button" class="btn btn-sm btn-outline" data-close>Cancelar</button><button class="btn btn-sm btn-primary">${esc(submitLabel)}</button></div></form>`,
      onMount: (el, close) => {
        el.querySelector('form').onsubmit = (e) => {
          e.preventDefault();
          done = true;
          resolve(Object.fromEntries(new FormData(e.target)));
          close();
        };
      },
    });
    const observer = new MutationObserver(() => {
      if (!document.querySelector('.modal-backdrop')) { observer.disconnect(); if (!done) resolve(null); }
    });
    observer.observe(document.getElementById('modal-root'), { childList: true });
  });
}

/** Lets the user pick a ready video from the library. Resolves with the video or null. */
export function pickVideo(title = 'Escolher vídeo') {
  return new Promise((resolve) => {
    let done = false;
    openModal({
      title,
      wide: true,
      body: `<label class="search-box">${icon('search')}<input placeholder="Buscar na biblioteca" data-q></label><div class="pick-list" data-list><div class="sys">Carregando…</div></div>`,
      onMount: (el, close) => {
        const list = el.querySelector('[data-list]');
        let timer;
        const load = async (search) => {
          const videos = (await api.videos.list({ search })).filter((v) => v.status === 'Ready');
          list.innerHTML = videos.length
            ? videos.map((v) => `<button class="pick" data-id="${v.id}">${thumb(v)}<div><div class="title">${esc(v.title)}</div><div class="sub">${esc(videoSubtitle(v))}</div></div></button>`).join('')
            : '<div class="sys">Nenhum vídeo pronto encontrado.</div>';
          list.querySelectorAll('[data-id]').forEach((b) => (b.onclick = () => {
            done = true;
            resolve(videos.find((v) => v.id === b.dataset.id));
            close();
          }));
        };
        el.querySelector('[data-q]').oninput = (e) => { clearTimeout(timer); timer = setTimeout(() => load(e.target.value), 250); };
        load('');
      },
    });
    const observer = new MutationObserver(() => {
      if (!document.querySelector('.modal-backdrop')) { observer.disconnect(); if (!done) resolve(null); }
    });
    observer.observe(document.getElementById('modal-root'), { childList: true });
  });
}

// ---------- Context menu ----------
/** items: [{ label, icon, onClick }] */
export function openMenu(anchor, items) {
  document.querySelector('.menu')?.remove();
  const menu = document.createElement('div');
  menu.className = 'menu';
  menu.innerHTML = items.map((item, i) => `<button data-i="${i}">${item.icon ? icon(item.icon) : ''}${esc(item.label)}</button>`).join('');
  document.body.appendChild(menu);

  const rect = anchor.getBoundingClientRect();
  const left = Math.min(rect.left, window.innerWidth - menu.offsetWidth - 8);
  const top = rect.bottom + menu.offsetHeight + 8 > window.innerHeight ? rect.top - menu.offsetHeight - 4 : rect.bottom + 4;
  Object.assign(menu.style, { left: `${Math.max(8, left)}px`, top: `${Math.max(8, top)}px` });

  const close = () => { menu.remove(); document.removeEventListener('mousedown', outside, true); };
  const outside = (e) => !menu.contains(e.target) && close();
  setTimeout(() => document.addEventListener('mousedown', outside, true));
  menu.addEventListener('click', (e) => {
    const button = e.target.closest('[data-i]');
    if (!button) return;
    close();
    items[Number(button.dataset.i)].onClick();
  });
}

/** Menu listing playlists to add a video to (plus "new playlist"). */
export async function addToPlaylistMenu(anchor, videoId) {
  const playlists = await api.playlists.list();
  const add = async (playlistId, name) => {
    try {
      await api.playlists.addVideo(playlistId, videoId);
      toast(`Adicionado a ${name}`, 'ok');
      window.dispatchEvent(new Event('library:changed'));
    } catch (err) { toast(err.message, 'error'); }
  };
  openMenu(anchor, [
    {
      label: 'Nova playlist', icon: 'add', onClick: async () => {
        const values = await formDialog('Nova playlist', [{ name: 'name', label: 'Nome', required: true, max: 100 }], 'Criar');
        if (!values) return;
        try {
          const { id } = await api.playlists.create({ name: values.name });
          await add(id, values.name);
        } catch (err) { toast(err.message, 'error'); }
      },
    },
    ...playlists.map((p) => ({ label: p.name, icon: 'queue', onClick: () => add(p.id, p.name) })),
  ]);
}

export function copyText(text) {
  if (navigator.clipboard?.writeText) return navigator.clipboard.writeText(text);
  const area = Object.assign(document.createElement('textarea'), { value: text });
  document.body.appendChild(area);
  area.select();
  document.execCommand('copy');
  area.remove();
  return Promise.resolve();
}

/** Tints the page header/top bar, like Spotify does with the cover's dominant color. */
export function setHero(color) {
  document.querySelector('.main')?.style.setProperty('--hero', color);
}
