// Website: three people watch together — upload, invite link, play/seek/pause sync, chat, voice, late joiner.
import { join } from 'node:path';
import { BASE, SHOTS, launch, newPage, ok, summary, testVideo, unique, videoState, webRegister } from '../lib.mjs';

const errors = [];
const browser = await launch();
const video = testVideo('sync');
const [will, ana, beto] = [unique('will'), unique('ana'), unique('beto')];

try {
  const a = await newPage(browser, 'A', { viewport: { width: 1440, height: 900 } }, errors);
  await webRegister(a, will, 'Willian');

  await a.goto(`${BASE}/#/upload`);
  await a.setInputFiles('input[type=file]', video);
  await a.waitForSelector('text=Enviado!', { timeout: 30000 });
  ok(true, 'upload via UI');

  const videoId = await a.getAttribute('.upload-item a[href^="#/video/"]', 'href');
  await a.goto(`${BASE}/${videoId}`);
  await a.waitForSelector('[data-watch]', { timeout: 15000 });
  await a.click('[data-watch]');
  await a.waitForSelector('.room video');
  const roomUrl = a.url();
  ok(/#\/room\/[A-Z0-9]{6}$/.test(roomUrl), `room created ${roomUrl}`);

  const b = await newPage(browser, 'B', { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true }, errors);
  await b.goto(roomUrl);
  await b.waitForSelector('.auth-card');
  await b.click('text=Cadastre-se com um convite');
  await b.fill('#displayName', 'Ana');
  await b.fill('#userName', ana);
  await b.fill('#password', 'senha123');
  await b.fill('#inviteCode', process.env.INVITE_CODE ?? 'amigos123');
  await b.click('button[type=submit]');
  await b.waitForSelector('.room video');
  ok(b.url() === roomUrl, 'invite link returns to the room after signing up');
  await a.waitForSelector('text=Ana entrou na sala');

  await a.waitForFunction(() => document.querySelector('.room video').readyState >= 1);
  await b.waitForFunction(() => document.querySelector('.room video').readyState >= 1);

  await a.evaluate(() => document.querySelector('.room video').play());
  await b.waitForTimeout(3000);
  let sa = await videoState(a), sb = await videoState(b);
  ok(!sb.paused && Math.abs(sa.t - sb.t) < 1, `play synced A=${sa.t.toFixed(2)} B=${sb.t.toFixed(2)}`);

  await a.evaluate(() => { document.querySelector('.room video').currentTime = 60; });
  await b.waitForTimeout(2500);
  sa = await videoState(a); sb = await videoState(b);
  ok(sb.t > 59 && Math.abs(sa.t - sb.t) < 1, `seek synced A=${sa.t.toFixed(2)} B=${sb.t.toFixed(2)}`);

  await b.evaluate(() => document.querySelector('.room video').pause());
  await a.waitForTimeout(1500);
  sa = await videoState(a); sb = await videoState(b);
  ok(sa.paused && Math.abs(sa.t - sb.t) < 0.7, `pause from B synced A.paused=${sa.paused}`);

  await b.fill('.chat-form input', 'Oi gente! 🍿');
  await b.press('.chat-form input', 'Enter');
  await a.waitForSelector('.msg .text:has-text("Oi gente!")', { timeout: 5000 });
  ok(true, 'chat message B -> A');
  await a.fill('.chat-form input', '<b>xss?</b>');
  await a.press('.chat-form input', 'Enter');
  await b.waitForSelector('.msg .text:has-text("<b>xss?</b>")', { timeout: 5000 });
  ok(true, 'chat escapes HTML');

  await a.click('[data-join-voice]');
  await a.waitForSelector('[data-leave-voice]');
  await b.click('[data-join-voice]');
  await b.waitForSelector('[data-leave-voice]');
  await a.waitForFunction(() => window.__pcs.some((pc) => pc.connectionState === 'connected'), null, { timeout: 15000 }).catch(() => {});
  const pcA = await a.evaluate(() => window.__pcs.map((pc) => pc.connectionState));
  const pcB = await b.evaluate(() => window.__pcs.map((pc) => pc.connectionState));
  ok(pcA.includes('connected') && pcB.includes('connected'), `voice connected A=${pcA} B=${pcB}`);
  ok((await a.textContent('.voice-bar .sub')).includes('2 na call'), 'voice bar counts both people');
  if (await a.evaluate(() => fetch('/api/config').then((r) => r.json()).then((c) => c.voice === 'livekit'))) {
    // LiveKit mode: audio goes through the LiveKit server and each side gets the other's track.
    await a.waitForSelector('audio[data-voice=livekit]', { state: 'attached', timeout: 15000 });
    await b.waitForSelector('audio[data-voice=livekit]', { state: 'attached', timeout: 15000 });
    ok(true, 'LiveKit audio tracks received on both sides');
  }
  await b.click('[data-mute]');
  await a.waitForFunction(() => document.querySelector('[data-people]').textContent.includes('mutado'), null, { timeout: 5000 });
  ok(true, 'mute shown to others');

  await a.evaluate(() => document.querySelector('.room video').play());
  await a.waitForTimeout(1500);
  await a.screenshot({ path: join(SHOTS, 'web-room-desktop.png') });
  await b.screenshot({ path: join(SHOTS, 'web-room-mobile.png') });

  await b.goto(`${BASE}/#/`);
  await a.waitForSelector('text=Ana saiu', { timeout: 5000 });
  ok(true, 'leaving the room notifies others');

  const c = await newPage(browser, 'C', { viewport: { width: 1280, height: 800 } }, errors);
  await webRegister(c, beto, 'Beto');
  await c.goto(roomUrl);
  await c.waitForSelector('.room video');
  await c.waitForFunction(() => document.querySelector('.room video').readyState >= 1);
  await c.waitForTimeout(2500);
  sa = await videoState(a);
  const sc = await videoState(c);
  ok(!sc.paused && Math.abs(sa.t - sc.t) < 1, `late joiner lands in sync A=${sa.t.toFixed(2)} C=${sc.t.toFixed(2)}`);
} catch (e) {
  ok(false, `crashed: ${e.message}`);
} finally {
  await browser.close();
}

ok(errors.length === 0, `no JS errors ${errors.length ? JSON.stringify(errors.slice(0, 5)) : ''}`);
const { passed, failed } = summary();
console.log(`web-sync: ${passed} passed, ${failed} failed`);
process.exit(failed ? 1 : 0);
