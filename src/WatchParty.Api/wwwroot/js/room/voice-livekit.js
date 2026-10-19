// Voice chat through a LiveKit server (optional, enabled by Voice:Provider=livekit on the server).
// Same interface as VoiceChat, so the room view doesn't care which one it got. The SignalR hub still
// tracks who is in the call/muted; LiveKit only carries the audio. LiveKit identity = connection id.

let loading = null;

function loadLiveKit() {
  if (window.LivekitClient) return Promise.resolve(window.LivekitClient);
  loading ??= new Promise((resolve, reject) => {
    const script = Object.assign(document.createElement('script'), { src: '/lib/livekit/livekit-client.umd.js' });
    script.onload = () => resolve(window.LivekitClient);
    script.onerror = () => { loading = null; reject(new Error('Não foi possível carregar o chat de voz.')); };
    document.head.append(script);
  });
  return loading;
}

export class LiveKitVoiceChat {
  constructor(hub, url, { onSpeaking, onPeerCountChange } = {}) {
    this.hub = hub;
    this.url = url;
    this.onSpeaking = onSpeaking;
    this.onPeerCountChange = onPeerCountChange;
    this.room = null;
    this.audios = new Map();   // track sid -> <audio>
    this.speaking = new Set();
    this.active = false;
    this.muted = false;
  }

  async join() {
    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error('Seu navegador bloqueou o microfone. O chat de voz precisa de HTTPS (ou localhost).');
    }
    const LK = await loadLiveKit();
    const token = await this.hub.invoke('GetVoiceToken');

    const room = new LK.Room({
      adaptiveStream: false,
      audioCaptureDefaults: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
    });
    this.room = room;

    room.on(LK.RoomEvent.TrackSubscribed, (track, publication) => {
      if (track.kind !== LK.Track.Kind.Audio) return;
      const audio = track.attach();
      audio.dataset.voice = 'livekit';
      document.body.append(audio);
      this.audios.set(publication.trackSid, audio);
    });
    room.on(LK.RoomEvent.TrackUnsubscribed, (track, publication) => {
      track.detach().forEach((a) => a.remove());
      this.audios.delete(publication.trackSid);
    });
    room.on(LK.RoomEvent.ActiveSpeakersChanged, (speakers) => {
      const now = new Set(speakers.map((p) => (p === room.localParticipant ? 'self' : p.identity)));
      for (const id of this.speaking) if (!now.has(id)) this.onSpeaking?.(id, false);
      for (const id of now) if (!this.speaking.has(id)) this.onSpeaking?.(id, true);
      this.speaking = now;
    });
    const count = () => this.onPeerCountChange?.(this.connectedCount());
    room.on(LK.RoomEvent.ParticipantConnected, count);
    room.on(LK.RoomEvent.ParticipantDisconnected, (p) => {
      if (this.speaking.delete(p.identity)) this.onSpeaking?.(p.identity, false);
      count();
    });
    room.on(LK.RoomEvent.AudioPlaybackStatusChanged, () => {
      if (!room.canPlaybackAudio) room.startAudio().catch(() => {});
    });

    await room.connect(this.url, token);
    this.active = true;
    this.muted = false;
    await room.localParticipant.setMicrophoneEnabled(true);
    await room.startAudio().catch(() => {});
    await this.hub.invoke('JoinVoice');
    count();
  }

  async leave({ notify = true } = {}) {
    const room = this.room;
    this.room = null;
    const wasActive = this.active;
    this.active = false;
    if (wasActive && notify) await this.hub.invoke('LeaveVoice').catch(() => {});
    for (const id of this.speaking) this.onSpeaking?.(id, false);
    this.speaking.clear();
    this.audios.forEach((a) => a.remove());
    this.audios.clear();
    await room?.disconnect().catch(() => {});
  }

  async setMuted(muted) {
    this.muted = muted;
    await this.room?.localParticipant.setMicrophoneEnabled(!muted).catch(() => {});
    await this.hub.invoke('SetMuted', muted).catch(() => {});
  }

  // LiveKit handles peers itself; these exist only to match VoiceChat.
  onParticipantUpdated() {}
  onParticipantLeft() {}
  async onSignal() {}

  connectedCount() {
    return this.room ? this.room.remoteParticipants.size : 0;
  }
}
