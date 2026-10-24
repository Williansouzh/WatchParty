// Renders every app icon PNG from the SVGs in this folder (uses the Playwright install in e2e/).
// Usage, from the repo root: CHROMIUM_PATH=... node branding/render-icons.mjs branding mobile src/WatchParty.Api/wwwroot
import { readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { createRequire } from 'node:module';
const require = createRequire(new URL('../e2e/package.json', import.meta.url));
const { chromium } = require('playwright');
const [brand, mobile, www] = process.argv.slice(2);
const svg = (n) => readFileSync(join(brand, n), 'utf8');
const jobs = [];
const res = join(mobile, 'android/app/src/main/res');
for (const [d, s] of Object.entries({ mdpi: 48, hdpi: 72, xhdpi: 96, xxhdpi: 144, xxxhdpi: 192 })) {
  jobs.push(['watchparty-icon.svg', s, join(res, `mipmap-${d}/ic_launcher.png`), true]);
}
const ios = join(mobile, 'ios/Runner/Assets.xcassets/AppIcon.appiconset');
for (const [n, s] of [['20x20@1x', 20], ['20x20@2x', 40], ['20x20@3x', 60], ['29x29@1x', 29], ['29x29@2x', 58], ['29x29@3x', 87],
  ['40x40@1x', 40], ['40x40@2x', 80], ['40x40@3x', 120], ['60x60@2x', 120], ['60x60@3x', 180], ['76x76@1x', 76], ['76x76@2x', 152],
  ['83.5x83.5@2x', 167], ['1024x1024@1x', 1024]]) jobs.push(['watchparty-icon-square.svg', s, join(ios, `Icon-App-${n}.png`), false]);
jobs.push(['watchparty-icon.svg', 32, join(mobile, 'web/favicon.png'), true]);
jobs.push(['watchparty-icon.svg', 192, join(mobile, 'web/icons/Icon-192.png'), true]);
jobs.push(['watchparty-icon.svg', 512, join(mobile, 'web/icons/Icon-512.png'), true]);
jobs.push(['watchparty-icon-maskable.svg', 192, join(mobile, 'web/icons/Icon-maskable-192.png'), false]);
jobs.push(['watchparty-icon-maskable.svg', 512, join(mobile, 'web/icons/Icon-maskable-512.png'), false]);
jobs.push(['watchparty-icon.svg', 180, join(www, 'apple-touch-icon.png'), false]);
jobs.push(['watchparty-icon.svg', 1024, join(brand, 'watchparty-icon-1024.png'), true]);
const li = join(ios, '../LaunchImage.imageset');
for (const [n, sz] of [['', 112], ['@2x', 224], ['@3x', 336]]) jobs.push(['watchparty-mark.svg', sz, join(li, `LaunchImage${n}.png`), true]);
jobs.push(['watchparty-mark.svg', 1024, join(brand, 'watchparty-mark-1024.png'), true]);

const browser = await chromium.launch({ executablePath: process.env.CHROMIUM_PATH, headless: false, args: ['--headless=new'] });
const page = await browser.newPage({ viewport: { width: 1100, height: 1100 } });
for (const [file, size, out, transparent] of jobs) {

  const body = svg(file).replace('<svg ', `<svg width="${size}" height="${size}" `);
  await page.setContent(`<html><body style="margin:0;background:${transparent ? 'transparent' : '#1ED760'}">${body}</body></html>`);
  writeFileSync(out, await page.screenshot({ omitBackground: transparent, clip: { x: 0, y: 0, width: size, height: size } }));
}
await browser.close();
console.log(`${jobs.length} icons`);
