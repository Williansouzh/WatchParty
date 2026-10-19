// Shared helpers for the browser tests.
//
// Environment:
//   BASE_URL        server under test (default http://localhost:5090). Run it WITHOUT ffprobe
//                   (Ffmpeg__FfprobePath=/nonexistent) so the WebM test videos play in Chromium,
//                   which has no H.264.
//   INVITE_CODE     the server's Auth:InviteCode (default amigos123)
//   CHROMIUM_PATH   optional browser executable
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

export const BASE = process.env.BASE_URL ?? 'http://localhost:5090';
export const INVITE = process.env.INVITE_CODE ?? 'amigos123';
const here = dirname(fileURLToPath(import.meta.url));
export const SHOTS = join(here, 'screenshots');
mkdirSync(SHOTS, { recursive: true });

const results = { passed: 0, failed: 0 };
export function ok(condition, message) {
  console.log(`${condition ? 'PASS' : 'FAIL'} ${message}`);
  condition ? results.passed++ : results.failed++;
}
export const summary = () => results;

export const unique = (name) => `${name}${Date.now().toString(36)}${Math.random().toString(36).slice(2, 5)}`;

/** Test videos are generated with ffmpeg (WebM/VP9, plays in every Chromium build). */
export function testVideo(name = 'clip', seconds = 120, pattern = 'testsrc2') {
  const dir = join(here, '.cache');
  mkdirSync(dir, { recursive: true });
  const file = join(dir, `${name}-${seconds}.webm`);
  if (!existsSync(file)) {
    execFileSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-y',
      '-f', 'lavfi', '-i', `${pattern}=size=640x360:rate=25`, '-f', 'lavfi', '-i', 'sine=frequency=440',
      '-t', String(seconds), '-c:v', 'libvpx-vp9', '-deadline', 'realtime', '-cpu-used', '8', '-b:v', '300k',
      '-c:a', 'libopus', file]);
  }
  return file;
}

export async function launch() {
  return chromium.launch({
    executablePath: process.env.CHROMIUM_PATH,
    headless: false,
    args: ['--headless=new', '--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream',
      '--autoplay-policy=no-user-gesture-required'],
  });
}

/** Records RTCPeerConnections so tests can check that voice actually connected. */
const trackPeerConnections = () => {
  window.__pcs = [];
  const Original = window.RTCPeerConnection;
  window.RTCPeerConnection = function (...args) { const pc = new Original(...args); window.__pcs.push(pc); return pc; };
  window.RTCPeerConnection.prototype = Original.prototype;
  Object.assign(window.RTCPeerConnection, Original);
};

export async function newPage(browser, name, options = {}, errors = []) {
  const context = await browser.newContext({ ...options, permissions: ['microphone', 'notifications'] });
  await context.addInitScript(trackPeerConnections);
  const page = await context.newPage();
  page.on('pageerror', (e) => errors.push(`${name}: ${e.message}`));
  page.on('console', (m) => {
    if (m.type() === 'error' && !/401|Failed to load resource/.test(m.text())) errors.push(`${name} console: ${m.text()}`);
  });
  return page;
}

/** Website (HTML client) sign-up through the UI. */
export async function webRegister(page, userName, displayName, path = '') {
  await page.goto(`${BASE}${path}#/register`);
  await page.fill('#displayName', displayName);
  await page.fill('#userName', userName);
  await page.fill('#password', 'senha123');
  await page.fill('#inviteCode', INVITE);
  await page.click('button[type=submit]');
  await page.waitForSelector('.layout');
}

/** REST helpers with a bearer token (the same API the Flutter app uses). */
export async function apiLogin(userName, password = 'senha123') {
  const response = await fetch(`${BASE}/api/auth/token/login`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName, password }),
  });
  if (!response.ok) throw new Error(`login failed: ${response.status}`);
  const { accessToken } = await response.json();
  const call = async (method, path, body) => {
    const r = await fetch(`${BASE}${path}`, {
      method,
      headers: { Authorization: `Bearer ${accessToken}`, ...(body && !(body instanceof FormData) ? { 'Content-Type': 'application/json' } : {}) },
      body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined,
    });
    const text = await r.text();
    if (!r.ok) throw new Error(`${method} ${path}: ${r.status} ${text}`);
    return text ? JSON.parse(text) : null;
  };
  return { token: accessToken, call };
}

export async function apiUpload(api, file, title) {
  const { readFile } = await import('node:fs/promises');
  const form = new FormData();
  form.append('title', title);
  form.append('file', new Blob([await readFile(file)], { type: 'video/webm' }), 'video.webm');
  const { id } = await api.call('POST', '/api/videos', form);
  for (let i = 0; i < 50; i++) {
    const video = await api.call('GET', `/api/videos/${id}`);
    if (video.status === 'Ready') return video;
    await new Promise((r) => setTimeout(r, 200));
  }
  throw new Error('video never became ready');
}

export const videoState = (page, selector = '.room video') =>
  page.evaluate((s) => {
    const v = document.querySelector(s);
    return v ? { t: v.currentTime, paused: v.paused, id: v.dataset.videoId, ready: v.readyState, duration: v.duration } : null;
  }, selector);
