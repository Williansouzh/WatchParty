// Voice chat as a WebRTC mesh: every participant connects directly to every other one.
// Great for a group of friends (up to ~6-8 people). SignalR only relays the handshake.

const SPEAKING_THRESHOLD = 0.04;

export class VoiceChat {
  constructor(hub, iceServers, { onSpeaking, onPeerCountChange } = {}) {
    this.hub = hub;
    this.iceServers = iceServers;
    this.onSpeaking = onSpeaking;
    this.onPeerCountChange = onPeerCountChange;
    this.peers = new Map();       // connectionId -> { pc, audio, pending: [] }
    this.meters = new Map();      // id -> { source, analyser, speaking }
    this.stream = null;
    this.active = false;
    this.muted = false;
  }

  async join() {
    if (!navigator.mediaDevices?.getUserMedia) {
      throw new Error('Seu navegador bloqueou o microfone. O chat de voz precisa de HTTPS (ou localhost).');
    }
    this.stream = await navigator.mediaDevices.getUserMedia({
      audio: { echoCancellation: true, noiseSuppression: true, autoGainControl: true },
      video: false,
    });
    this.active = true;
    this.muted = false;
    this.audioContext = new (window.AudioContext || window.webkitAudioContext)();
    await this.audioContext.resume().catch(() => {});
    this.meter('self', this.stream);
    this.meterTimer = setInterval(() => this.updateMeters(), 150);

    const others = await this.hub.invoke('JoinVoice');
    for (const other of others) await this.call(other.connectionId);
  }

  async leave({ notify = true } = {}) {
    if (!this.active) return;
    this.active = false;
    if (notify) await this.hub.invoke('LeaveVoice').catch(() => {});
    for (const id of [...this.peers.keys()]) this.closePeer(id);
    this.stream?.getTracks().forEach((t) => t.stop());
    this.stream = null;
    clearInterval(this.meterTimer);
    this.meters.clear();
    await this.audioContext?.close().catch(() => {});
    this.audioContext = null;
  }

  async setMuted(muted) {
    this.muted = muted;
    this.stream?.getAudioTracks().forEach((t) => (t.enabled = !muted));
    await this.hub.invoke('SetMuted', muted).catch(() => {});
  }

  // ----- events forwarded by the room view -----

  onParticipantUpdated(participant) {
    if (!participant.inVoice) this.closePeer(participant.connectionId);
  }

  onParticipantLeft(connectionId) {
    this.closePeer(connectionId);
  }

  async onSignal(from, type, payload) {
    if (!this.active) return;
    const data = JSON.parse(payload);
    const peer = this.peers.get(from) ?? this.createPeer(from);

    try {
      if (type === 'offer') {
        await peer.pc.setRemoteDescription(data);
        await this.flushCandidates(peer);
        await peer.pc.setLocalDescription(await peer.pc.createAnswer());
        this.send(from, 'answer', peer.pc.localDescription);
      } else if (type === 'answer') {
        await peer.pc.setRemoteDescription(data);
        await this.flushCandidates(peer);
      } else if (type === 'ice') {
        if (peer.pc.remoteDescription) await peer.pc.addIceCandidate(data);
        else peer.pending.push(data);
      }
    } catch (err) {
      console.warn('voice signal failed', type, err);
    }
  }

  // ----- internals -----

  async call(connectionId) {
    const peer = this.createPeer(connectionId);
    await peer.pc.setLocalDescription(await peer.pc.createOffer());
    this.send(connectionId, 'offer', peer.pc.localDescription);
  }

  createPeer(connectionId) {
    const pc = new RTCPeerConnection({ iceServers: this.iceServers });
    const audio = new Audio();
    audio.autoplay = true;
    audio.playsInline = true;
    const peer = { pc, audio, pending: [] };
    this.peers.set(connectionId, peer);

    this.stream?.getTracks().forEach((track) => pc.addTrack(track, this.stream));
    pc.onicecandidate = (e) => e.candidate && this.send(connectionId, 'ice', e.candidate);
    pc.ontrack = (e) => {
      const [remote] = e.streams;
      audio.srcObject = remote;
      audio.play().catch(() => {});
      this.meter(connectionId, remote);
    };
    pc.onconnectionstatechange = () => {
      if (pc.connectionState === 'failed') console.warn('voice connection failed with', connectionId, '— a TURN server may be needed');
      this.onPeerCountChange?.(this.connectedCount());
    };
    return peer;
  }

  async flushCandidates(peer) {
    for (const candidate of peer.pending.splice(0)) {
      await peer.pc.addIceCandidate(candidate).catch(() => {});
    }
  }

  closePeer(connectionId) {
    const peer = this.peers.get(connectionId);
    if (!peer) return;
    peer.pc.close();
    peer.audio.srcObject = null;
    this.peers.delete(connectionId);
    this.meters.delete(connectionId);
    this.onSpeaking?.(connectionId, false);
    this.onPeerCountChange?.(this.connectedCount());
  }

  connectedCount() {
    return [...this.peers.values()].filter((p) => p.pc.connectionState === 'connected').length;
  }

  send(to, type, data) {
    this.hub.invoke('SendVoiceSignal', to, type, JSON.stringify(data)).catch((err) => console.warn('signal', err));
  }

  meter(id, stream) {
    if (!this.audioContext) return;
    try {
      const source = this.audioContext.createMediaStreamSource(stream);
      const analyser = this.audioContext.createAnalyser();
      analyser.fftSize = 512;
      source.connect(analyser);
      this.meters.set(id, { source, analyser, data: new Uint8Array(analyser.fftSize), speaking: false });
    } catch { /* metering is cosmetic */ }
  }

  updateMeters() {
    for (const [id, m] of this.meters) {
      m.analyser.getByteTimeDomainData(m.data);
      let sum = 0;
      for (const v of m.data) sum += ((v - 128) / 128) ** 2;
      const speaking = Math.sqrt(sum / m.data.length) > SPEAKING_THRESHOLD && !(id === 'self' && this.muted);
      if (speaking !== m.speaking) {
        m.speaking = speaking;
        this.onSpeaking?.(id, speaking);
      }
    }
  }
}
