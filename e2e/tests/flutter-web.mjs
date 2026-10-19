// Flutter app (web build, phone-sized) and the website in the same room.
// Needs a server whose web root has the Flutter build at "/" and the site at "/site.html" (see combine-webroot.sh).
import { join } from 'node:path';
import { BASE, SHOTS, apiLogin, apiUpload, launch, newPage, ok, summary, testVideo, unique, webRegister } from '../lib.mjs';

const errors = [];
const browser = await launch();
const [will, ana] = [unique('will'), unique('ana')];

/** Flutter renders to a canvas; its accessibility tree gives us buttons and inputs to drive. */
const enableSemantics = async (p) => {
  await p.evaluate(() => document.querySelector('flt-semantics-placeholder')?.click());
  await p.waitForTimeout(500);
};
const tapAt = async (p, locator) => {
  const box = await locator.boundingBox({ timeout: 10000 });
  await p.mouse.click(box.x + box.width / 2, box.y + box.height / 2);
};
const button = (p, name) =>
  p.locator('flt-semantics[role=button]', { hasText: name }).or(p.locator(`flt-semantics[role=button][aria-label="${name}"]`)).first();
const typeInto = async (p, label, text) => {
  await tapAt(p, p.getByLabel(label));
  await p.waitForTimeout(300);
  await p.keyboard.type(text, { delay: 15 });
  await p.waitForTimeout(200);
};
const flutterVideo = (p) => p.evaluate(() => {
  const v = document.querySelector('video');
  return v ? { t: v.currentTime, paused: v.paused } : null;
});
const webVideo = (p) => p.evaluate(() => {
  const v = document.querySelector('.room video');
  return { t: v.currentTime, paused: v.paused };
});

try {
  // Willian uses the website and hosts; Ana uses the Flutter app on her phone.
  const w = await newPage(browser, 'WEB', { viewport: { width: 1280, height: 800 } }, errors);
  await webRegister(w, will, 'Willian', '/site.html');
  const willApi = await apiLogin(will);
  const clip = await apiUpload(willApi, testVideo('flutter'), 'Filme do app');
  const { code } = await willApi.call('POST', '/api/rooms', { name: 'App + site', videoId: clip.id });

  // Ana opens the invite link while logged out: login first, then straight into the room.
  const f = await newPage(browser, 'FLUTTER', { viewport: { width: 412, height: 915 }, deviceScaleFactor: 2, isMobile: true, hasTouch: true }, errors);
  await f.goto(`${BASE}/#/room/${code}`);
  await f.waitForSelector('flt-semantics-placeholder', { state: 'attached', timeout: 30000 });
  await f.waitForTimeout(1500);
  await enableSemantics(f);
  await tapAt(f, button(f, 'Não tem conta? Cadastre-se com um convite'));
  await f.waitForTimeout(800);
  await enableSemantics(f);
  await typeInto(f, 'Seu nome', 'Ana');
  await typeInto(f, 'Usuário', ana);
  await typeInto(f, 'Senha', 'senha123');
  await typeInto(f, 'Código de convite', process.env.INVITE_CODE ?? 'amigos123');
  await tapAt(f, button(f, 'Criar conta'));
  await f.waitForTimeout(5000);
  await enableSemantics(f);
  ok(f.url().includes(`/room/${code}`), `invite link continues into the room after signing up (${f.url()})`);

  await w.evaluate((c) => { location.hash = `#/room/${c}`; }, code);
  await w.waitForSelector('.room video');
  await w.waitForFunction(() => document.querySelector('.room video').readyState >= 1);
  await f.waitForTimeout(1500);
  await enableSemantics(f);

  // Flutter presses play -> website plays
  await tapAt(f, button(f, 'Play'));
  await w.waitForTimeout(3000);
  let a = await flutterVideo(f), b = await webVideo(w);
  ok(a && !a.paused && !b.paused && Math.abs(a.t - b.t) < 1, `app play -> site plays: F=${a?.t.toFixed(2)} W=${b.t.toFixed(2)}`);

  // Website seeks and pauses -> app follows
  await w.evaluate(() => { document.querySelector('.room video').currentTime = 60; });
  await f.waitForTimeout(2500);
  a = await flutterVideo(f); b = await webVideo(w);
  ok(a.t > 59 && Math.abs(a.t - b.t) < 1, `site seek -> app seeks: F=${a.t.toFixed(2)} W=${b.t.toFixed(2)}`);
  await w.evaluate(() => document.querySelector('.room video').pause());
  await f.waitForTimeout(2000);
  a = await flutterVideo(f);
  ok(a.paused, 'site pause -> app pauses');

  // Chat both ways
  await w.fill('.chat-form input', 'Oi do navegador 👋');
  await w.press('.chat-form input', 'Enter');
  await f.waitForTimeout(1500);
  await enableSemantics(f);
  ok(await f.getByText('Oi do navegador 👋').count() > 0, 'site chat -> app');
  await typeInto(f, 'Mande uma mensagem', 'Oi do app Flutter');
  await tapAt(f, button(f, 'Enviar mensagem'));
  await w.waitForSelector('.msg .text:has-text("Oi do app Flutter")', { timeout: 5000 })
    .then(() => ok(true, 'app chat -> site')).catch(() => ok(false, 'app chat -> site'));

  // Reaction from the app floats on the website
  await enableSemantics(f);
  await tapAt(f, button(f, 'Reagir 🔥'));
  await w.waitForSelector('.reaction-float:has-text("🔥")', { timeout: 5000 })
    .then(() => ok(true, 'app reaction shows on the site')).catch(() => ok(false, 'app reaction shows on the site'));

  // Queue: the site adds a video, the app's queue tab counts it
  const second = await apiUpload(willApi, testVideo('flutter2', 30, 'mandelbrot'), 'Segundo');
  await w.click('[data-tab="queue"]');
  await w.click('[data-queue-add]');
  await w.click('.pick:has-text("Segundo")');
  await f.waitForTimeout(1500);
  await enableSemantics(f);
  ok(await f.getByText('Fila (1)').count() > 0, 'queue change reaches the app');
  ok(second.id.length > 0, 'second video uploaded');

  // Host-only: the site host locks controls; the app shows the lock
  await w.check('[data-host-only]');
  await f.waitForTimeout(1500);
  await enableSemantics(f);
  ok(await f.getByText(/só Willian/).count() > 0, 'app shows that only the host controls');

  // Voice between the app and the site
  await tapAt(f, button(f, 'Entrar'));
  await f.waitForTimeout(1500);
  await w.click('[data-join-voice]');
  await w.waitForFunction(() => window.__pcs.some((pc) => pc.connectionState === 'connected'), null, { timeout: 15000 }).catch(() => {});
  await f.waitForFunction(() => window.__pcs.some((pc) => pc.connectionState === 'connected'), null, { timeout: 15000 }).catch(() => {});
  const pf = await f.evaluate(() => window.__pcs.map((p) => p.connectionState));
  const pw = await w.evaluate(() => window.__pcs.map((p) => p.connectionState));
  ok(pf.includes('connected') && pw.includes('connected'), `voice app <-> site: F=${pf} W=${pw}`);
  await enableSemantics(f);
  await tapAt(f, button(f, 'Mutar'));
  await w.waitForFunction(() => document.querySelector('[data-people]').textContent.includes('mutado'), null, { timeout: 5000 })
    .then(() => ok(true, 'app mute shows on the site')).catch(() => ok(false, 'app mute shows on the site'));

  await f.screenshot({ path: join(SHOTS, 'flutter-room.png') });
  await enableSemantics(f);
  await tapAt(f, f.getByText(/^Fila/).first());
  await f.waitForTimeout(800);
  await f.screenshot({ path: join(SHOTS, 'flutter-queue.png') });

  await enableSemantics(f);
  await tapAt(f, button(f, 'Sair da sala'));
  await w.waitForFunction(() => document.querySelector('[data-chat]').textContent.includes('Ana saiu'), null, { timeout: 6000 })
    .then(() => ok(true, 'leaving from the app notifies the site')).catch(() => ok(false, 'leaving from the app notifies the site'));
} catch (e) {
  ok(false, `crashed: ${e.stack}`);
} finally {
  await browser.close();
}

const relevant = errors.filter((e) => !/WebGL|GPU stall|GroupMarker/.test(e));
ok(relevant.length === 0, `no JS errors ${relevant.length ? JSON.stringify(relevant.slice(0, 5)) : ''}`);
const { passed, failed } = summary();
console.log(`flutter-web: ${passed} passed, ${failed} failed`);
process.exit(failed ? 1 : 0);
