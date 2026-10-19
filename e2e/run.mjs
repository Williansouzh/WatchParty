// Runs the browser tests in sequence. Usage: node run.mjs [test names...]
import { spawnSync } from 'node:child_process';

const all = ['web-sync', 'web-features', 'flutter-web'];
const names = process.argv.slice(2).length ? process.argv.slice(2) : all;
let failed = false;
for (const name of names) {
  console.log(`\n=== ${name} ===`);
  const result = spawnSync(process.execPath, [`tests/${name}.mjs`], { stdio: 'inherit', env: process.env });
  if (result.status !== 0) failed = true;
}
process.exit(failed ? 1 : 0);
