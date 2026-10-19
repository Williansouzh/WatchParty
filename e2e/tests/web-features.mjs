// Website: subtitles, reactions, queue auto-advance, waiting for whoever is loading, host-only control,
// deleted video, lobby alerts and "sign out everywhere".
import { join } from 'node:path';
import {
  BASE, SHOTS, apiLogin, apiUpload, launch, newPage, ok, summary, testVideo, unique, videoState, webRegister,
} from '../lib.mjs';

const errors = [];
const browser = await launch();
const [will, ana, beto] = [unique('will'), unique('ana'), unique('beto')];
const shortClip = testVideo('short', 12, 'testsrc2');
const longClip = testVideo('long', 120, 'mandelbrot');

try {
  // Willian (desktop) and Ana (desktop) sign up; Beto just browses the home page.
  const a = await newPage(browser, 'A', { viewport: { width: 1440, height: 900 } }, errors);
  await webRegister(a, will, 'Willian');
  const b = await newPage(browser, 'B', { viewport: { width: 1280, height: 800 } }, errors);
  await webRegister(b, ana, 'Ana');
  const c = await newPage(browser, 'C', { viewport: { width: 1280, height: 800 } }, errors);
  await webRegister(c, beto, 'Beto');

  const willApi = await apiLogin(will);
  const first = await apiUpload(willApi, shortClip, 'Curta');
  const second = await apiUpload(willApi, longClip, 'Longa');
  await willApi.call('POST', `/api/videos/${first.id}/subtitles`, (() => {
    const form = new FormData();
    form.append('label', 'Português');
    form.append('language', 'pt');
    form.append('file', new Blob(['1\n00:00:01,000 --> 00:00:05,000\nLegenda de teste\n']), 'teste.srt');
    return form;
  })());

  // ----- Lobby: Beto hears about the new room -----
  await c.goto(`${BASE}/#/`);
  await c.waitForTimeout(1500);
  const { code } = await willApi.call('POST', '/api/rooms', { name: 'Sessão de teste', videoId: first.id });
  await c.waitForSelector('.toast:has-text("criou a sala")', { timeout: 8000 })
    .then(() => ok(true, 'lobby alert about the new room'))
    .catch(() => ok(false, 'lobby alert about the new room'));

  await a.goto(`${BASE}/#/room/${code}`);
  await a.waitForSelector('.room video');
  await b.goto(`${BASE}/#/room/${code}`);
  await b.waitForSelector('.room video');
  await a.waitForSelector('text=Ana entrou na sala');

  // ----- Subtitles -----
  const tracks = await b.evaluate(() => [...document.querySelectorAll('.room video track')].map((t) => t.label));
  ok(tracks.includes('Português'), `subtitle track offered in the player: ${tracks}`);

  // ----- Reactions -----
  await a.click('[data-reaction="🔥"]');
  await b.waitForSelector('.reaction-float:has-text("🔥")', { timeout: 5000 })
    .then(() => ok(true, 'reaction floats over the other person\'s player'))
    .catch(() => ok(false, 'reaction floats over the other person\'s player'));

  // ----- Queue + auto-advance -----
  await a.click('[data-tab="queue"]');
  await a.click('[data-queue-add]');
  await a.waitForSelector('.pick:has-text("Longa")');
  await a.click('.pick:has-text("Longa")');
  await b.waitForFunction(() => document.querySelector('[data-queue-count]').textContent.includes('(1)'), null, { timeout: 5000 });
  ok(true, 'queue update reaches everyone');

  await a.waitForFunction(() => document.querySelector('.room video').readyState >= 1);
  await a.evaluate(() => { const v = document.querySelector('.room video'); v.currentTime = Math.max(0, v.duration - 2); v.play(); });
  await b.waitForFunction((id) => document.querySelector('.room video').dataset.videoId === id, second.id, { timeout: 15000 })
    .then(() => ok(true, 'queue advanced to the next video when the first ended'))
    .catch(() => ok(false, 'queue advanced to the next video when the first ended'));
  await a.waitForTimeout(3000);
  const afterAdvance = await videoState(b);
  ok(!afterAdvance.paused, 'next video starts playing by itself');
  ok(await b.locator('.sys:has-text("Próximo da fila")').count() > 0, 'chat explains the queue advanced');

  // ----- Waiting for whoever is loading -----
  // Ana's connection becomes very slow and Willian jumps far ahead: Ana can't load, the room waits for her.
  const slow = await b.context().newCDPSession(b);
  await slow.send('Network.enable');
  await slow.send('Network.emulateNetworkConditions', { offline: false, latency: 400, downloadThroughput: 6 * 1024, uploadThroughput: 6 * 1024 });
  await b.evaluate(() => {
    // Drop what was already buffered so the jump really needs the network.
    const v = document.querySelector('.room video');
    const t = v.currentTime; v.src = v.src; v.currentTime = t;
  });
  await a.evaluate(() => { document.querySelector('.room video').currentTime = 95; });
  await a.waitForSelector('[data-waiting]:not(.hidden)', { timeout: 30000 })
    .then(() => ok(true, 'room waits: "Esperando Ana carregar…" shown'))
    .catch(() => ok(false, 'room waits: "Esperando Ana carregar…" shown'));
  const waitingText = await a.textContent('[data-waiting]');
  ok(waitingText.includes('Ana'), `waiting banner names the person: ${waitingText.trim()}`);
  ok((await videoState(a)).paused, 'everyone else is paused while waiting');
  await a.screenshot({ path: join(SHOTS, 'web-waiting.png') });

  await slow.send('Network.emulateNetworkConditions', { offline: false, latency: 0, downloadThroughput: -1, uploadThroughput: -1 });
  await a.waitForSelector('[data-waiting].hidden', { state: 'attached', timeout: 30000 })
    .then(() => ok(true, 'room resumes by itself when everyone is ready'))
    .catch(() => ok(false, 'room resumes by itself when everyone is ready'));
  await a.waitForTimeout(2000);
  const [ra, rb] = [await videoState(a), await videoState(b)];
  ok(!ra.paused && !rb.paused && Math.abs(ra.t - rb.t) < 1.5, `playing together again A=${ra.t.toFixed(1)} B=${rb.t.toFixed(1)}`);

  // ----- Host-only control -----
  await a.check('[data-host-only]');
  await b.waitForSelector('[data-lock]:has-text("Só")', { timeout: 5000 });
  ok(true, 'guests see that only the host controls');
  await b.evaluate(() => document.querySelector('.room video').pause());
  await b.waitForSelector('.toast:has-text("só o anfitrião")', { timeout: 5000 })
    .then(() => ok(true, 'guest pause is refused with a message'))
    .catch(() => ok(false, 'guest pause is refused with a message'));
  await b.waitForTimeout(1500);
  ok(!(await videoState(b)).paused, 'guest player goes back to the room state');
  ok(!(await videoState(a)).paused, 'host kept playing');
  await a.uncheck('[data-host-only]');

  // ----- Deleted video -----
  await willApi.call('DELETE', `/api/videos/${second.id}`);
  await b.waitForSelector('.toast:has-text("foi apagado")', { timeout: 5000 })
    .then(() => ok(true, 'room is told when its video is deleted'))
    .catch(() => ok(false, 'room is told when its video is deleted'));
  ok(await b.locator('[data-empty]:not(.hidden)').count() === 1, 'player shows "no video" instead of breaking');

  // ----- Sign out everywhere -----
  const anaApi = await apiLogin(ana);
  await anaApi.call('POST', '/api/auth/logout-everywhere');
  await b.goto(`${BASE}/#/library`);
  await b.waitForSelector('.auth-card', { timeout: 8000 })
    .then(() => ok(true, 'browser session ended after "sair de todos os aparelhos"'))
    .catch(() => ok(false, 'browser session ended after "sair de todos os aparelhos"'));
  const stillValid = await fetch(`${BASE}/api/auth/me`, { headers: { Authorization: `Bearer ${anaApi.token}` } });
  ok(stillValid.status === 401, 'app token revoked too');

  await a.screenshot({ path: join(SHOTS, 'web-features-desktop.png') });
} catch (e) {
  ok(false, `crashed: ${e.stack}`);
} finally {
  await browser.close();
}

ok(errors.length === 0, `no JS errors ${errors.length ? JSON.stringify(errors.slice(0, 5)) : ''}`);
const { passed, failed } = summary();
console.log(`web-features: ${passed} passed, ${failed} failed`);
process.exit(failed ? 1 : 0);
