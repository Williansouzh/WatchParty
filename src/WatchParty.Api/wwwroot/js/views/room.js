// The watch party itself: synced player (MP4 or adaptive HLS), subtitles, queue, waiting for whoever is
// loading, reactions, text chat, people list and voice call.
import { api } from '../api.js';
import {
  confirmDialog, copyText, esc, formatClock, icon, openModal, pickVideo, playlistCover, thumb, timeOfDay, toast,
} from '../ui.js';
import { PlaybackSync } from '../room/sync.js';
import { VoiceChat } from '../room/voice.js';

const ACTION_TEXT = { Play: 'deu play', Pause: 'pausou', Seek: 'pulou para' };
const REACTIONS = ['😂', '😍', '🔥', '😱', '👏', '😭', '👍', '🍿'];
const ANDROID_PACKAGE = 'com.watchparty.watchparty';

export async function render(root, ctx) {
  const code = ctx.params.code.toUpperCase();
  root.innerHTML = '<div class="page"><span class="spinner"></span> Entrando na sala…</div>';

  const config = await api.config().catch(() => ({ iceServers: [] }));
  const hub = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/room')
    .withAutomaticReconnect()
    .configureLogging(signalR.LogLevel.Warning)
    .build();

  const participants = new Map();
  const speaking = new Set();
  let room = null;
  let queue = [];
  let waitingFor = [];
  let myConnectionId = null;
  let sync = null;
  let voice = null;
  let hls = null;
  let disposed = false;
  let activeTab = 'chat';
  let unread = 0;
  let el = {};

  const isHost = () => room?.hostId === ctx.user.id;
  const canControl = () => !room?.hostOnlyControl || isHost();

  // ---------- Hub events ----------
  hub.on('ParticipantJoined', (p) => {
    participants.set(p.connectionId, p);
    systemMessage(`${p.displayName} entrou na sala`);
    renderPeople();
  });
  hub.on('ParticipantLeft', (connectionId) => {
    const p = participants.get(connectionId);
    participants.delete(connectionId);
    speaking.delete(connectionId);
    voice?.onParticipantLeft(connectionId);
    if (p) systemMessage(`${p.displayName} saiu`);
    renderPeople();
  });
  hub.on('ParticipantUpdated', (p) => {
    const before = participants.get(p.connectionId);
    participants.set(p.connectionId, p);
    if (before && before.inVoice !== p.inVoice) systemMessage(`${p.displayName} ${p.inVoice ? 'entrou na' : 'saiu da'} call`);
    voice?.onParticipantUpdated(p);
    renderPeople();
  });
  hub.on('PlaybackChanged', (e) => {
    sync?.apply(e.state);
    if (e.reason === 'buffering') systemMessage('⏸ Pausado para esperar quem está carregando');
    else if (e.reason === 'resumed') systemMessage('▶ Todo mundo pronto, continuando');
    else systemMessage(`${e.byDisplayName} ${ACTION_TEXT[e.action]} ${formatClock(e.state.positionSeconds)}`);
  });
  hub.on('VideoChanged', (e) => {
    queue = e.queue;
    loadVideo(e.video, e.state);
    renderQueue();
    systemMessage(e.reason === 'next' ? `▶ Próximo da fila: "${e.video.title}"` : `${e.byDisplayName} colocou "${e.video.title}"`);
  });
  hub.on('VideoRemoved', (e) => {
    loadVideo(null, e.state);
    systemMessage('O vídeo foi apagado da biblioteca por quem enviou.');
    toast('O vídeo desta sala foi apagado.', 'error');
  });
  hub.on('QueueChanged', (items) => { queue = items; renderQueue(); });
  hub.on('RoomSettingsChanged', (s) => {
    room.hostOnlyControl = s.hostOnlyControl;
    systemMessage(s.hostOnlyControl ? `🔒 ${s.byDisplayName} deixou só o anfitrião controlar o vídeo` : `🔓 Todo mundo pode controlar o vídeo`);
    renderControls();
  });
  hub.on('BufferingChanged', (names) => { waitingFor = names; renderWaiting(); });
  hub.on('Reaction', (r) => showReaction(r.emoji, r.displayName));
  hub.on('ChatMessage', (m) => {
    appendMessage(m);
    if (activeTab !== 'chat' || document.hidden) { unread++; renderUnread(); }
  });
  hub.on('VoiceSignal', (from, type, payload) => voice?.onSignal(from, type, payload));
  hub.on('RoomDeleted', () => {
    if (disposed) return;
    toast('A sala foi encerrada.', 'error');
    location.hash = '#/rooms';
  });

  hub.onreconnecting(() => systemMessage('Conexão instável, reconectando…'));
  hub.onreconnected(async () => {
    if (voice?.active) {
      await voice.leave({ notify: false });
      voice = null;
      toast('A call caiu. Entre de novo na voz.', 'error');
    }
    await join();
    systemMessage('Reconectado ✓');
  });
  hub.onclose(() => { if (!disposed) systemMessage('Desconectado do servidor. Recarregue a página.'); });

  // ---------- Start ----------
  try {
    await hub.start();
  } catch {
    root.innerHTML = '<div class="page empty-state"><h3>Não deu para conectar</h3><p>Verifique sua internet e tente de novo.</p></div>';
    return;
  }

  let joined;
  try {
    joined = await hub.invoke('JoinRoom', code);
  } catch (err) {
    await hub.stop();
    root.innerHTML = `<div class="page empty-state"><h3>Sala não encontrada</h3><p>${esc(cleanHubError(err))}</p>
      <a class="btn btn-primary" href="#/rooms" style="margin-top:16px">Ver salas</a></div>`;
    return;
  }
  if (!ctx.isCurrent()) { await hub.stop(); return; }

  room = joined.room;
  drawLayout();
  sync = new PlaybackSync(el.player, hub, {
    onAutoplayBlocked: () => el.gesture.classList.remove('hidden'),
    onError: (err) => toast(cleanHubError(err), 'error'),
    // Every player reports the end; the server advances the queue only once.
    onEnded: () => room.currentVideo && hub.invoke('VideoEnded', room.currentVideo.id).catch(() => {}),
  });
  applyJoin(joined);
  sync.calibrate(joined.serverTimeMs).catch(() => {});

  async function join() {
    const response = await hub.invoke('JoinRoom', code);
    applyJoin(response);
    sync.calibrate(response.serverTimeMs).catch(() => {});
  }

  function applyJoin(response) {
    room = response.room;
    queue = room.queue;
    waitingFor = response.waitingFor;
    myConnectionId = response.connectionId;
    participants.clear();
    response.participants.forEach((p) => participants.set(p.connectionId, p));
    el.chat.innerHTML = '';
    response.messages.forEach(appendMessage);
    systemMessage('Você entrou. Tudo que alguém fizer no player acontece pra todo mundo.');
    loadVideo(room.currentVideo, room.playback);
    renderPeople();
    renderQueue();
    renderControls();
    renderWaiting();
  }

  // ---------- Layout ----------
  function drawLayout() {
    const android = /Android/i.test(navigator.userAgent);
    root.innerHTML = `
      <div class="room">
        <section class="room-stage">
          ${android ? `<div class="app-banner">${icon('play')}Assista pelo app
            <a href="intent://app/room/${esc(room.code)}#Intent;scheme=watchparty;package=${ANDROID_PACKAGE};end">Abrir</a></div>` : ''}
          <div class="player-wrap" data-player-wrap>
            <video playsinline controls preload="auto"></video>
            <div class="player-empty hidden" data-empty></div>
            <div class="waiting-banner hidden" data-waiting></div>
            <button class="gesture-overlay hidden" data-gesture><span>${icon('play')}Toque para entrar na sessão</span></button>
          </div>
          <div class="room-info">
            <div>
              <h1>${esc(room.name)}</h1>
              <p><span data-video-title></span> • Código <span class="code-pill">${esc(room.code)}</span> <span data-lock></span></p>
            </div>
            <div class="actions">
              <button class="btn btn-sm btn-primary" data-invite>${icon('link')}Convidar</button>
              <button class="btn btn-sm btn-outline" data-change>${icon('swap')}Trocar vídeo</button>
              ${isHost() ? `<button class="btn btn-sm btn-danger" data-delete>${icon('trash')}Encerrar</button>` : ''}
            </div>
          </div>
          <div style="display:flex;align-items:center;justify-content:space-between;gap:12px;flex-wrap:wrap">
            <div class="reaction-bar" data-reactions>${REACTIONS.map((r) => `<button data-reaction="${r}" title="Reagir">${r}</button>`).join('')}</div>
            ${isHost() ? `<label class="switch"><input type="checkbox" data-host-only> Só eu controlo o vídeo</label>` : ''}
          </div>
        </section>
        <aside class="room-side">
          <div class="tabs">
            <button class="chip active" data-tab="chat">${icon('chat')}&nbsp;Chat<span class="unread hidden" data-unread></span></button>
            <button class="chip" data-tab="people">${icon('group')}&nbsp;<span data-people-count>Pessoas</span></button>
            <button class="chip" data-tab="queue">${icon('queue')}&nbsp;<span data-queue-count>Fila</span></button>
          </div>
          <div class="tab-body" data-panel="chat">
            <div class="chat-list" data-chat></div>
            <form class="chat-form" data-chat-form>
              <input maxlength="1000" placeholder="Mande uma mensagem" autocomplete="off" enterkeyhint="send">
              <button title="Enviar">${icon('send')}</button>
            </form>
          </div>
          <div class="tab-body hidden" data-panel="people"><div class="people" data-people></div></div>
          <div class="tab-body hidden" data-panel="queue">
            <div class="queue-actions">
              <button class="btn btn-sm btn-outline" data-queue-add>${icon('add')}Vídeo</button>
              <button class="btn btn-sm btn-outline" data-queue-playlist>${icon('queue')}Playlist</button>
              <button class="btn btn-sm btn-primary" data-queue-next>${icon('forward')}Próximo</button>
            </div>
            <div class="queue" data-queue></div>
          </div>
        </aside>
      </div>`;

    el = {
      player: root.querySelector('video'),
      wrap: root.querySelector('[data-player-wrap]'),
      empty: root.querySelector('[data-empty]'),
      waiting: root.querySelector('[data-waiting]'),
      gesture: root.querySelector('[data-gesture]'),
      videoTitle: root.querySelector('[data-video-title]'),
      lock: root.querySelector('[data-lock]'),
      chat: root.querySelector('[data-chat]'),
      people: root.querySelector('[data-people]'),
      peopleCount: root.querySelector('[data-people-count]'),
      queue: root.querySelector('[data-queue]'),
      queueCount: root.querySelector('[data-queue-count]'),
      unread: root.querySelector('[data-unread]'),
      hostOnly: root.querySelector('[data-host-only]'),
      bar: document.getElementById('player-bar'),
    };

    el.gesture.addEventListener('click', () => {
      el.gesture.classList.add('hidden');
      sync.resumeFromGesture();
    });

    root.querySelectorAll('[data-tab]').forEach((tab) => tab.addEventListener('click', () => {
      activeTab = tab.dataset.tab;
      root.querySelectorAll('[data-tab]').forEach((t) => t.classList.toggle('active', t === tab));
      root.querySelectorAll('[data-panel]').forEach((p) => p.classList.toggle('hidden', p.dataset.panel !== activeTab));
      if (activeTab === 'chat') { unread = 0; renderUnread(); scrollChat(true); }
    }));

    root.querySelector('[data-chat-form]').addEventListener('submit', async (e) => {
      e.preventDefault();
      const input = e.target.querySelector('input');
      const content = input.value.trim();
      if (!content) return;
      input.value = '';
      try { await hub.invoke('SendMessage', content); } catch (err) { input.value = content; toast(cleanHubError(err), 'error'); }
    });

    root.querySelector('[data-reactions]').addEventListener('click', (e) => {
      const button = e.target.closest('[data-reaction]');
      if (button) hub.invoke('SendReaction', button.dataset.reaction).catch((err) => toast(cleanHubError(err), 'error'));
    });

    el.hostOnly?.addEventListener('change', () => {
      hub.invoke('SetHostOnlyControl', el.hostOnly.checked).catch((err) => {
        el.hostOnly.checked = !el.hostOnly.checked;
        toast(cleanHubError(err), 'error');
      });
    });

    root.querySelector('[data-invite]').addEventListener('click', async () => {
      const url = `${location.origin}/#/room/${room.code}`;
      if (navigator.share && matchMedia('(pointer: coarse)').matches) {
        navigator.share({ title: room.name, text: `Bora assistir junto? Sala ${room.code}`, url }).catch(() => {});
      } else {
        await copyText(url);
        toast('Link da sala copiado!', 'ok');
      }
    });

    root.querySelector('[data-change]').addEventListener('click', async () => {
      const video = await pickVideo('Trocar vídeo da sala');
      if (!video) return;
      try {
        const changed = await hub.invoke('ChangeVideo', video.id);
        queue = changed.queue;
        loadVideo(changed.video, changed.playback);
        renderQueue();
        systemMessage(`Você colocou "${changed.video.title}"`);
      } catch (err) { toast(cleanHubError(err), 'error'); }
    });

    root.querySelector('[data-queue-add]').addEventListener('click', async () => {
      const video = await pickVideo('Adicionar à fila');
      if (!video) return;
      try {
        queue = await hub.invoke('AddToQueue', video.id);
        renderQueue();
        toast(`"${video.title}" entrou na fila`, 'ok');
      } catch (err) { toast(cleanHubError(err), 'error'); }
    });

    root.querySelector('[data-queue-playlist]').addEventListener('click', async () => {
      const playlist = await pickPlaylist();
      if (!playlist) return;
      try {
        queue = await hub.invoke('AddPlaylistToQueue', playlist.id);
        renderQueue();
        toast(`Playlist "${playlist.name}" adicionada à fila`, 'ok');
      } catch (err) { toast(cleanHubError(err), 'error'); }
    });

    root.querySelector('[data-queue-next]').addEventListener('click', () =>
      hub.invoke('PlayNext').catch((err) => toast(cleanHubError(err), 'error')));

    el.queue.addEventListener('click', async (e) => {
      const remove = e.target.closest('[data-queue-remove]');
      if (!remove) return;
      try {
        queue = await hub.invoke('RemoveFromQueue', remove.dataset.queueRemove);
        renderQueue();
      } catch (err) { toast(cleanHubError(err), 'error'); }
    });

    root.querySelector('[data-delete]')?.addEventListener('click', async () => {
      if (!await confirmDialog('Encerrar sala?', 'Todo mundo será desconectado e o chat será apagado.', 'Encerrar')) return;
      try {
        disposed = true;
        await api.rooms.remove(room.code);
        location.hash = '#/rooms';
      } catch (err) { disposed = false; toast(err.message, 'error'); }
    });

    document.addEventListener('visibilitychange', onVisibility);
  }

  function onVisibility() {
    if (!document.hidden && sync?.state) sync.apply(sync.state);
  }

  // ---------- Player ----------
  async function loadVideo(video, state) {
    room.currentVideo = video;
    const player = el.player;
    if (!video || video.status !== 'Ready') {
      destroyHls();
      player.pause();
      player.removeAttribute('src');
      player.removeAttribute('data-video-id');
      player.querySelectorAll('track').forEach((t) => t.remove());
      player.load();
      el.empty.innerHTML = video
        ? '<div><span class="spinner"></span><p>Esse vídeo ainda está sendo preparado…</p></div>'
        : `<div><p>Nenhum vídeo na sala.</p><button class="btn btn-sm btn-primary" data-pick>${icon('swap')}Escolher vídeo</button></div>`;
      el.empty.querySelector('[data-pick]')?.addEventListener('click', () => root.querySelector('[data-change]').click());
      el.empty.classList.remove('hidden');
      el.videoTitle.textContent = video ? video.title : 'Sem vídeo';
      sync.state = state;
      renderVoiceBar();
      return;
    }

    el.empty.classList.add('hidden');
    el.videoTitle.textContent = video.title;
    if (player.dataset.videoId !== video.id) {
      player.dataset.videoId = video.id;
      player.poster = video.hasThumbnail ? api.videos.thumbUrl(video.id) : '';
      sync.videoLoaded();
      await attachSource(video);
      setSubtitles(video);
    }
    sync.apply(state); // waits for metadata if needed
    renderVoiceBar();
  }

  /** Adaptive HLS when available (hls.js, or natively on Safari/iOS); otherwise the MP4. */
  async function attachSource(video) {
    const player = el.player;
    destroyHls();
    if (video.hasHls) {
      if (player.canPlayType('application/vnd.apple.mpegurl')) {
        player.src = api.videos.hlsUrl(video.id);
        return;
      }
      const Hls = await loadHlsJs();
      if (Hls?.isSupported()) {
        hls = new Hls({ capLevelToPlayerSize: true });
        hls.on(Hls.Events.ERROR, (_, data) => {
          if (data.fatal) { destroyHls(); player.src = api.videos.streamUrl(video.id); player.load(); }
        });
        hls.loadSource(api.videos.hlsUrl(video.id));
        hls.attachMedia(player);
        return;
      }
    }
    player.src = api.videos.streamUrl(video.id);
    player.load();
  }

  function destroyHls() {
    hls?.destroy();
    hls = null;
  }

  /** Subtitle tracks are per person (not synced): everyone picks theirs in the player's CC menu. */
  function setSubtitles(video) {
    el.player.querySelectorAll('track').forEach((t) => t.remove());
    for (const s of video.subtitles ?? []) {
      const track = Object.assign(document.createElement('track'), {
        kind: 'subtitles', label: s.label, srclang: s.language ?? '', src: api.videos.subtitleUrl(video.id, s.id),
      });
      el.player.appendChild(track);
    }
  }

  function renderWaiting() {
    if (!el.waiting) return;
    el.waiting.classList.toggle('hidden', waitingFor.length === 0);
    el.waiting.innerHTML = waitingFor.length
      ? `<span class="spinner"></span>Esperando ${esc(waitingFor.join(', '))} carregar…`
      : '';
  }

  function renderControls() {
    el.lock.textContent = room.hostOnlyControl ? `🔒 Só ${room.hostName} controla` : '';
    if (el.hostOnly) el.hostOnly.checked = room.hostOnlyControl;
    root.querySelector('[data-change]').classList.toggle('hidden', !canControl());
    renderQueue();
  }

  function showReaction(emoji, name) {
    const item = document.createElement('div');
    item.className = 'reaction-float';
    item.style.left = `${10 + Math.random() * 75}%`;
    item.innerHTML = `${esc(emoji)}<small>${esc(name)}</small>`;
    el.wrap.appendChild(item);
    setTimeout(() => item.remove(), 2700);
  }

  // ---------- Queue ----------
  function renderQueue() {
    if (!el.queue) return;
    el.queueCount.textContent = queue.length ? `Fila (${queue.length})` : 'Fila';
    root.querySelector('[data-queue-next]').classList.toggle('hidden', !queue.length || !canControl());
    el.queue.innerHTML = queue.length
      ? queue.map((item, i) => `
          <div class="queue-item">
            <span style="color:var(--text-sub);width:18px;text-align:right">${i + 1}</span>
            ${thumb(item.video)}
            <div class="meta"><div class="title">${esc(item.video.title)}</div><div class="sub">sugerido por ${esc(item.addedByName)}</div></div>
            ${item.addedById === ctx.user.id || canControl()
              ? `<button class="icon-btn" data-queue-remove="${item.itemId}" title="Tirar da fila">${icon('close')}</button>` : ''}
          </div>`).join('')
      : '<div class="sys" style="padding:24px">A fila está vazia. Adicione vídeos para emendar um no outro.</div>';
  }

  function pickPlaylist() {
    return new Promise((resolve) => {
      let done = false;
      openModal({
        title: 'Adicionar playlist à fila',
        body: '<div class="pick-list" data-list><div class="sys">Carregando…</div></div>',
        onMount: async (modal, close) => {
          const list = modal.querySelector('[data-list]');
          const playlists = await api.playlists.list();
          list.innerHTML = playlists.length
            ? playlists.map((p) => `<button class="pick" data-id="${p.id}">${playlistCover(p, 'thumb')}<div><div class="title">${esc(p.name)}</div><div class="sub">${p.itemCount} vídeo(s)</div></div></button>`).join('')
            : '<div class="sys">Nenhuma playlist ainda.</div>';
          list.querySelectorAll('[data-id]').forEach((b) => (b.onclick = () => {
            done = true;
            resolve(playlists.find((p) => p.id === b.dataset.id));
            close();
          }));
        },
      });
      const observer = new MutationObserver(() => {
        if (!document.querySelector('.modal-backdrop')) { observer.disconnect(); if (!done) resolve(null); }
      });
      observer.observe(document.getElementById('modal-root'), { childList: true });
    });
  }

  // ---------- Chat ----------
  function appendMessage(m) {
    const last = el.chat.lastElementChild;
    const grouped = last?.dataset.user === m.userId && new Date(m.sentAt) - new Date(last.dataset.time) < 3 * 60_000;
    const nearBottom = isNearBottom();
    const item = document.createElement('div');
    item.className = `msg${grouped ? ' grouped' : ''}`;
    item.dataset.user = m.userId;
    item.dataset.time = m.sentAt;
    item.innerHTML = `
      <span class="avatar" style="background:${esc(m.avatarColor)}">${esc(m.displayName[0]?.toUpperCase())}</span>
      <div style="min-width:0"><div class="who">${esc(m.displayName)}<time>${timeOfDay(m.sentAt)}</time></div><div class="text">${esc(m.content)}</div></div>`;
    el.chat.appendChild(item);
    scrollChat(nearBottom || m.userId === ctx.user.id);
  }

  function systemMessage(text) {
    if (!el.chat) return;
    const nearBottom = isNearBottom();
    const item = document.createElement('div');
    item.className = 'sys';
    item.textContent = text;
    el.chat.appendChild(item);
    scrollChat(nearBottom);
  }

  function isNearBottom() {
    return el.chat.scrollHeight - el.chat.scrollTop - el.chat.clientHeight < 80;
  }

  function scrollChat(force) {
    if (force) el.chat.scrollTop = el.chat.scrollHeight;
  }

  function renderUnread() {
    el.unread.textContent = unread;
    el.unread.classList.toggle('hidden', unread === 0);
  }

  // ---------- People & voice ----------
  function personAvatar(p) {
    return `<span class="avatar ${speaking.has(p.connectionId) ? 'speaking' : ''} ${p.isMuted ? 'muted' : ''}"
      data-conn="${esc(p.connectionId)}" style="background:${esc(p.avatarColor)}" title="${esc(p.displayName)}">${esc(p.displayName[0]?.toUpperCase())}</span>`;
  }

  function personStatus(p) {
    if (p.isBuffering) return 'Carregando…';
    if (p.inVoice) return p.isMuted ? 'Na call • mutado' : 'Na call';
    return 'Assistindo';
  }

  function renderPeople() {
    if (!el.people) return;
    const list = [...participants.values()].sort((a, b) => Number(b.inVoice) - Number(a.inVoice) || a.displayName.localeCompare(b.displayName));
    const users = new Set(list.map((p) => p.userId)).size;
    el.peopleCount.textContent = `Pessoas (${users})`;
    el.people.innerHTML = list.map((p) => `
      <div class="person">
        ${personAvatar(p)}
        <div class="name">${esc(p.displayName)}${p.connectionId === myConnectionId ? ' (você)' : ''}${p.userId === room.hostId ? ' 👑' : ''}
          <small>${personStatus(p)}</small></div>
        ${p.inVoice ? icon(p.isMuted ? 'micOff' : 'mic') : ''}
      </div>`).join('');
    renderVoiceBar();
  }

  function renderVoiceBar() {
    if (!el.bar || disposed) return;
    const inVoice = !!voice?.active;
    const voiceList = [...participants.values()].filter((p) => p.inVoice);
    const watching = new Set([...participants.values()].map((p) => p.userId)).size;
    const video = room.currentVideo;
    el.bar.innerHTML = `
      <div class="voice-bar">
        <div class="now">
          ${video ? thumb(video) : `<div class="thumb placeholder">${icon('live')}</div>`}
          <div style="min-width:0">
            <div class="title">${esc(video?.title ?? room.name)}</div>
            <div class="sub"><span class="live-dot"></span>${watching} assistindo${voiceList.length ? ` • ${voiceList.length} na call` : ''}</div>
          </div>
        </div>
        <div class="voice-controls">
          ${inVoice ? `
            <button class="ctrl ${voice.muted ? 'muted' : ''}" data-mute title="${voice.muted ? 'Ativar microfone' : 'Mutar'}">${icon(voice.muted ? 'micOff' : 'mic')}</button>
            <button class="ctrl hangup" data-leave-voice title="Sair da call">${icon('hangup')}</button>`
          : `<button class="btn btn-sm btn-light join" data-join-voice>${icon('headset')}Entrar na call</button>`}
        </div>
        <div class="voice-people">${voiceList.map(personAvatar).join('')}</div>
      </div>`;

    el.bar.querySelector('[data-join-voice]')?.addEventListener('click', joinVoice);
    el.bar.querySelector('[data-leave-voice]')?.addEventListener('click', async () => {
      await voice?.leave();
      voice = null;
      updateSelf({ inVoice: false, isMuted: false });
    });
    el.bar.querySelector('[data-mute]')?.addEventListener('click', async () => {
      await voice.setMuted(!voice.muted);
      updateSelf({ isMuted: voice.muted });
    });
  }

  /** The server only tells the *others* about our changes, so mirror them locally. */
  function updateSelf(changes) {
    const me = participants.get(myConnectionId);
    if (me) participants.set(myConnectionId, { ...me, ...changes });
    renderPeople();
  }

  async function joinVoice() {
    if (voice?.active) return;
    voice = new VoiceChat(hub, config.iceServers, {
      onSpeaking: (id, isSpeaking) => {
        const connectionId = id === 'self' ? myConnectionId : id;
        if (isSpeaking) speaking.add(connectionId); else speaking.delete(connectionId);
        document.querySelectorAll(`[data-conn="${CSS.escape(connectionId)}"]`).forEach((a) => a.classList.toggle('speaking', isSpeaking));
      },
    });
    try {
      await voice.join();
      updateSelf({ inVoice: true, isMuted: false });
      toast('Você está na call. Use fone para evitar eco 🎧', 'ok');
    } catch (err) {
      await voice.leave({ notify: false }).catch(() => {});
      voice = null;
      toast(err.name === 'NotAllowedError' ? 'Permita o acesso ao microfone para entrar na call.' : (err.message || 'Não foi possível entrar na call.'), 'error');
    }
    renderPeople();
  }

  // ---------- Cleanup ----------
  return async () => {
    disposed = true;
    document.removeEventListener('visibilitychange', onVisibility);
    if (el.bar) el.bar.innerHTML = '';
    sync?.dispose();
    destroyHls();
    await voice?.leave().catch(() => {});
    el.player?.pause();
    el.player?.removeAttribute('src');
    el.player?.load();
    await hub.invoke('LeaveRoom').catch(() => {});
    await hub.stop().catch(() => {});
  };
}

let hlsJsPromise = null;

/** hls.js is ~400 KB, so it is only downloaded when a room actually needs it. */
function loadHlsJs() {
  if (window.Hls) return Promise.resolve(window.Hls);
  hlsJsPromise ??= new Promise((resolve) => {
    const script = Object.assign(document.createElement('script'), { src: '/lib/hls/hls.min.js' });
    script.onload = () => resolve(window.Hls);
    script.onerror = () => { hlsJsPromise = null; resolve(null); };
    document.head.appendChild(script);
  });
  return hlsJsPromise;
}

function cleanHubError(err) {
  // SignalR prefixes server messages with "An unexpected error occurred invoking 'X' on the server. HubException: ..."
  const message = err?.message ?? String(err);
  const marker = 'HubException: ';
  return message.includes(marker) ? message.slice(message.indexOf(marker) + marker.length) : message;
}
