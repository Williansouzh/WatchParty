// Entry point: auth bootstrap, hash router and the Spotify-like shell (sidebar, top bar, bottom nav).
import { api } from './api.js';
import { avatar, esc, icon, openMenu, playlistCover, toast, formDialog } from './ui.js';
import * as auth from './views/auth.js';
import * as home from './views/home.js';
import * as search from './views/search.js';
import * as library from './views/library.js';
import * as video from './views/video.js';
import * as playlist from './views/playlist.js';
import * as upload from './views/upload.js';
import * as rooms from './views/rooms.js';
import * as room from './views/room.js';

const routes = [
  { path: '/', view: home, nav: 'home' },
  { path: '/search', view: search, nav: 'search' },
  { path: '/library', view: library, nav: 'library' },
  { path: '/liked', view: library.liked, nav: 'library' },
  { path: '/upload', view: upload, nav: 'upload' },
  { path: '/rooms', view: rooms, nav: 'rooms' },
  { path: '/video/:id', view: video },
  { path: '/playlist/:id', view: playlist, nav: 'library' },
  { path: '/room/:code', view: room, nav: 'rooms', roomMode: true },
];

const app = document.getElementById('app');
export const state = { user: null };
let cleanup = null;
let renderToken = 0;

function match(hash) {
  const path = hash.replace(/^#/, '') || '/';
  const [pathname, query = ''] = path.split('?');
  for (const route of routes) {
    const names = [];
    const pattern = new RegExp('^' + route.path.replace(/:(\w+)/g, (_, n) => (names.push(n), '([^/]+)')) + '/?$');
    const m = pathname.match(pattern);
    if (m) {
      const params = Object.fromEntries(names.map((n, i) => [n, decodeURIComponent(m[i + 1])]));
      return { route, params, query: new URLSearchParams(query) };
    }
  }
  return null;
}

async function navigate() {
  const token = ++renderToken;
  if (cleanup) { try { await cleanup(); } catch { /* view already gone */ } cleanup = null; }
  document.querySelector('.menu')?.remove();

  const hash = location.hash || '#/';

  if (hash.startsWith('#/login') || hash.startsWith('#/register')) {
    if (state.user) { location.hash = '#/'; return; }
    app.innerHTML = '';
    cleanup = (await auth.render(app, { mode: hash.startsWith('#/register') ? 'register' : 'login', onAuthenticated }));
    return;
  }

  if (!state.user) {
    sessionStorage.setItem('returnTo', hash);
    location.hash = '#/login';
    return;
  }

  const found = match(hash) ?? match('#/');
  ensureShell();
  setActiveNav(found.route.nav, hash);

  const scroll = document.getElementById('main-scroll');
  // Fresh container per page so listeners from the previous view are dropped with it.
  const view = document.createElement('div');
  view.id = 'view';
  document.getElementById('view').replaceWith(view);
  document.getElementById('player-bar').innerHTML = '';
  scroll.classList.toggle('room-mode', !!found.route.roomMode);
  scroll.scrollTop = 0;
  document.querySelector('.main').style.removeProperty('--hero');

  const ctx = { user: state.user, params: found.params, query: found.query, isCurrent: () => token === renderToken };
  try {
    const result = await found.route.view.render(view, ctx);
    if (token === renderToken) cleanup = typeof result === 'function' ? result : null;
    else if (typeof result === 'function') result();
  } catch (err) {
    console.error(err);
    if (token === renderToken) view.innerHTML = `<div class="page empty-state"><h3>Ops!</h3><p>${esc(err.message)}</p></div>`;
  }
}

function ensureShell() {
  if (document.querySelector('.layout')) return;
  const u = state.user;
  app.innerHTML = `
    <div class="layout">
      <aside class="sidebar">
        <nav class="panel nav">
          <a class="brand" href="#/"><span class="logo">${icon('play')}</span>WatchParty</a>
          <a href="#/" data-nav="home">${icon('home')}Início</a>
          <a href="#/search" data-nav="search">${icon('search')}Buscar</a>
          <a href="#/rooms" data-nav="rooms">${icon('live')}Salas</a>
          <a href="#/upload" data-nav="upload">${icon('upload')}Enviar vídeo</a>
        </nav>
        <section class="panel library">
          <div class="library-head">
            <h2><a href="#/library" style="display:flex;gap:12px;align-items:center">${icon('library')}Sua Biblioteca</a></h2>
            <button class="icon-btn" data-new-playlist title="Criar playlist">${icon('add')}</button>
          </div>
          <div class="library-list" id="sidebar-library"></div>
        </section>
      </aside>
      <main class="panel main">
        <header class="topbar" id="topbar">
          <div class="group">
            <button class="round-btn" onclick="history.back()" title="Voltar">${icon('back')}</button>
            <button class="round-btn hide-mobile" onclick="history.forward()" title="Avançar">${icon('forward')}</button>
          </div>
          <div class="group">
            <a class="btn btn-sm btn-light hide-mobile" href="#/upload">${icon('upload')}Enviar</a>
            <button class="avatar-btn" data-user-menu title="${esc(u.displayName)}">${avatar(u.displayName, u.avatarColor)}</button>
          </div>
        </header>
        <div class="main-scroll" id="main-scroll"><div id="view"></div></div>
      </main>
      <div class="player-bar" id="player-bar"></div>
      <nav class="bottom-nav">
        <a href="#/" data-nav="home">${icon('home')}Início</a>
        <a href="#/search" data-nav="search">${icon('search')}Buscar</a>
        <a href="#/library" data-nav="library">${icon('library')}Biblioteca</a>
        <a href="#/rooms" data-nav="rooms">${icon('live')}Salas</a>
        <a href="#/upload" data-nav="upload">${icon('upload')}Enviar</a>
      </nav>
    </div>`;

  const scroll = document.getElementById('main-scroll');
  const topbar = document.getElementById('topbar');
  scroll.addEventListener('scroll', () => topbar.classList.toggle('solid', scroll.scrollTop > 40), { passive: true });

  app.querySelector('[data-user-menu]').addEventListener('click', (e) => openMenu(e.currentTarget, [
    { label: `${u.displayName} (@${u.userName})`, icon: 'group', onClick: () => {} },
    { label: 'Sair', icon: 'logout', onClick: logout },
  ]));
  app.querySelector('[data-new-playlist]').addEventListener('click', createPlaylist);

  renderSidebarLibrary();
}

export async function createPlaylist() {
  const values = await formDialog('Nova playlist', [
    { name: 'name', label: 'Nome', required: true, max: 100 },
    { name: 'description', label: 'Descrição (opcional)', type: 'textarea', max: 500 },
  ], 'Criar');
  if (!values) return;
  try {
    const { id } = await api.playlists.create(values);
    window.dispatchEvent(new Event('library:changed'));
    location.hash = `#/playlist/${id}`;
  } catch (err) { toast(err.message, 'error'); }
}

async function renderSidebarLibrary() {
  const list = document.getElementById('sidebar-library');
  if (!list) return;
  try {
    const playlists = await api.playlists.list();
    list.innerHTML = `
      <a class="lib-item" href="#/liked" data-lib="#/liked">
        <div class="cover cover-liked">${icon('heart')}</div>
        <div class="meta"><div class="title">Vídeos curtidos</div><div class="sub">Coleção</div></div>
      </a>
      ${playlists.map((p) => `
        <a class="lib-item" href="#/playlist/${p.id}" data-lib="#/playlist/${p.id}">
          ${playlistCover(p)}
          <div class="meta"><div class="title">${esc(p.name)}</div><div class="sub">Playlist • ${esc(p.ownerName)}</div></div>
        </a>`).join('')}`;
    setActiveNav(null, location.hash);
  } catch { /* sidebar is best-effort */ }
}

function setActiveNav(nav, hash) {
  if (nav !== null) document.querySelectorAll('[data-nav]').forEach((a) => a.classList.toggle('active', a.dataset.nav === nav));
  document.querySelectorAll('[data-lib]').forEach((a) => a.classList.toggle('active', a.dataset.lib === hash));
}

function onAuthenticated(user) {
  state.user = user;
  const returnTo = sessionStorage.getItem('returnTo');
  sessionStorage.removeItem('returnTo');
  location.hash = returnTo && !returnTo.startsWith('#/login') && !returnTo.startsWith('#/register') ? returnTo : '#/';
}

async function logout() {
  if (cleanup) { await cleanup(); cleanup = null; }
  await api.logout().catch(() => {});
  state.user = null;
  app.innerHTML = '';
  location.hash = '#/login';
}

window.addEventListener('library:changed', renderSidebarLibrary);
window.addEventListener('auth:expired', () => {
  if (!state.user) return;
  state.user = null;
  app.innerHTML = '';
  sessionStorage.setItem('returnTo', location.hash);
  location.hash = '#/login';
});
window.addEventListener('hashchange', navigate);

(async function boot() {
  try { state.user = await api.me(); } catch { state.user = null; }
  navigate();
})();
