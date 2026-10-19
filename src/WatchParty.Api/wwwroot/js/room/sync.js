// Keeps a <video> in step with the room's authoritative playback state on the server.
//
// The server stores "position P at server time T, playing or paused". Every client computes
// where the video should be now, using its estimated offset to the server clock.

const HARD_SEEK_THRESHOLD = 2.0;   // seconds off: jump
const SOFT_SYNC_THRESHOLD = 0.35;  // seconds off: nudge playbackRate
const APPLY_SEEK_THRESHOLD = 0.6;  // when applying a remote action
const DRIFT_CHECK_MS = 4000;
const SUPPRESS_MS = 900;
const BUFFERING_REPORT_MS = 2500;   // stuck loading this long -> ask the room to wait
const LOAD_GRACE_MS = 8000;         // right after (re)loading a video, don't make everyone wait

export class PlaybackSync {
  constructor(video, hub, { onLocalAction, onAutoplayBlocked, onError, onEnded }) {
    this.video = video;
    this.hub = hub;
    this.state = null;
    this.clockOffset = 0;
    this.suppressUntil = 0;
    this.pendingLocal = 0;
    this.lastLocalAt = 0;
    this.onLocalAction = onLocalAction;
    this.onAutoplayBlocked = onAutoplayBlocked;
    this.onError = onError;
    this.onEnded = onEnded;
    this.bufferingReported = false;
    this.bufferingTimer = null;
    this.loadedAt = performance.now();

    this.handlers = {
      play: () => this.local('Play'),
      pause: () => { if (!this.video.ended) this.local('Pause'); },
      seeked: () => this.local('Seek'),
      loadedmetadata: () => this.state && this.apply(this.state, { force: true }),
      ended: () => this.onEnded?.(),
      waiting: () => this.scheduleBufferingReport(),
      stalled: () => this.scheduleBufferingReport(),
      playing: () => this.checkRecovered(),
      canplay: () => this.checkRecovered(),
      canplaythrough: () => this.checkRecovered(),
    };
    for (const [event, handler] of Object.entries(this.handlers)) video.addEventListener(event, handler);

    this.driftTimer = setInterval(() => this.checkDrift(), DRIFT_CHECK_MS);
  }

  /** Estimates server clock offset with a few round trips, keeping the fastest (least noisy) one. */
  async calibrate(initialServerTime) {
    if (initialServerTime) this.clockOffset = initialServerTime - Date.now();
    let best = Infinity;
    for (let i = 0; i < 4; i++) {
      const t0 = Date.now();
      const server = await this.hub.invoke('GetServerTime');
      const t1 = Date.now();
      if (t1 - t0 < best) { best = t1 - t0; this.clockOffset = server - (t0 + t1) / 2; }
    }
  }

  serverNow() { return Date.now() + this.clockOffset; }

  expectedPosition(state = this.state) {
    if (!state) return 0;
    const elapsed = state.isPlaying ? (this.serverNow() - state.updatedAtMs) / 1000 : 0;
    let position = state.positionSeconds + Math.max(0, elapsed);
    if (Number.isFinite(this.video.duration)) position = Math.min(position, this.video.duration);
    return position;
  }

  /** Applies a state that came from the server (another person's action or a resync). */
  apply(state, { force = false } = {}) {
    // Never go back to an older state (e.g. a remote event that lost the race against our own action).
    if (this.state && state.videoId === this.state.videoId && state.updatedAtMs < this.state.updatedAtMs) state = this.state;
    this.state = state;
    const video = this.video;
    if (!video.src || video.readyState < 1) return; // loadedmetadata will call us again

    const target = this.expectedPosition(state);
    this.suppress();
    video.playbackRate = 1;

    if (force || Math.abs(video.currentTime - target) > APPLY_SEEK_THRESHOLD) video.currentTime = target;

    if (state.isPlaying && video.paused) this.tryPlay();
    else if (!state.isPlaying && !video.paused) video.pause();
  }

  tryPlay() {
    this.suppress();
    const attempt = this.video.play();
    attempt?.catch((err) => {
      if (err.name === 'NotAllowedError') this.onAutoplayBlocked?.();
    });
  }

  /** Called from a user gesture after autoplay was blocked. */
  resumeFromGesture() {
    if (this.state) this.apply(this.state, { force: true });
  }

  async local(action) {
    if (performance.now() < this.suppressUntil || !this.state?.videoId) return;
    const position = this.video.currentTime;

    // Ignore echoes of a state we already have (e.g. "play" fired while the room is already playing).
    if (action === 'Play' && this.state.isPlaying && Math.abs(this.expectedPosition() - position) < APPLY_SEEK_THRESHOLD) return;
    if (action === 'Pause' && !this.state.isPlaying && Math.abs(this.state.positionSeconds - position) < APPLY_SEEK_THRESHOLD) return;
    if (action === 'Seek' && Math.abs(this.expectedPosition() - position) < APPLY_SEEK_THRESHOLD) return;

    this.pendingLocal++;
    this.lastLocalAt = performance.now();
    try {
      this.state = await this.hub.invoke('Playback', action, position);
      this.onLocalAction?.(action, position);
    } catch (err) {
      this.onError?.(err);
      // The server refused (e.g. only the host controls): put the player back where the room is.
      if (this.state) setTimeout(() => this.apply(this.state, { force: true }), 0);
    } finally {
      this.pendingLocal--;
    }
  }

  /** Call when a new video starts loading. */
  videoLoaded() {
    this.loadedAt = performance.now();
    clearTimeout(this.bufferingTimer);
    if (this.bufferingReported) this.reportBuffering(false);
  }

  scheduleBufferingReport(delay = BUFFERING_REPORT_MS) {
    clearTimeout(this.bufferingTimer);
    this.bufferingTimer = setTimeout(() => {
      const stuck = this.video.readyState < 3 && this.state?.isPlaying && this.state?.videoId;
      if (!stuck || this.bufferingReported) return;
      // Still in the grace period after loading: check again when it ends instead of giving up.
      const sinceLoad = performance.now() - this.loadedAt;
      if (sinceLoad < LOAD_GRACE_MS) this.scheduleBufferingReport(LOAD_GRACE_MS - sinceLoad + 100);
      else this.reportBuffering(true);
    }, delay);
  }

  checkRecovered() {
    if (this.video.readyState >= 3) {
      clearTimeout(this.bufferingTimer);
      if (this.bufferingReported) this.reportBuffering(false);
    }
  }

  reportBuffering(buffering) {
    this.bufferingReported = buffering;
    this.hub.invoke('SetBuffering', buffering).catch(() => {});
  }

  async checkDrift() {
    this.checkRecovered(); // safety net: some browsers skip "canplay" while paused
    if (document.hidden || !this.state?.videoId || this.video.readyState < 2 || this.video.seeking || this.pendingLocal) return;
    const startedAt = performance.now();
    let fresh;
    try {
      fresh = await this.hub.invoke('GetPlayback');
    } catch { return; }

    // Our own action (or a newer event) happened while the request was in flight: this answer is stale.
    if (this.pendingLocal || this.lastLocalAt > startedAt || this.video.seeking) return;
    if (fresh.videoId === this.state?.videoId && fresh.updatedAtMs < this.state.updatedAtMs) return;
    this.state = fresh;

    const video = this.video;
    if (this.state.isPlaying !== !video.paused) { this.apply(this.state); return; }
    if (!this.state.isPlaying) return;

    // Positive = we are behind.
    const drift = this.expectedPosition() - video.currentTime;
    if (Math.abs(drift) > HARD_SEEK_THRESHOLD) {
      this.suppress();
      video.currentTime = this.expectedPosition();
      video.playbackRate = 1;
    } else if (Math.abs(drift) > SOFT_SYNC_THRESHOLD) {
      video.playbackRate = drift > 0 ? 1.06 : 0.94;
    } else {
      video.playbackRate = 1;
    }
  }

  suppress() { this.suppressUntil = performance.now() + SUPPRESS_MS; }

  dispose() {
    clearInterval(this.driftTimer);
    clearTimeout(this.bufferingTimer);
    for (const [event, handler] of Object.entries(this.handlers)) this.video.removeEventListener(event, handler);
  }
}
