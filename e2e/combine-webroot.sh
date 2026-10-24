#!/usr/bin/env bash
# Builds a web root with the Flutter web build at "/" and the HTML site at "/site.html", so both clients
# can talk to the same server in the cross-client test. Usage: ./combine-webroot.sh <output-dir>
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:?output dir}"
site="$here/../src/WatchParty.Api/wwwroot"
rm -rf "$out" && mkdir -p "$out"
cp -r "$here/../mobile/build/web/." "$out/"
cp -r "$site/css" "$site/js" "$site/lib" "$out/"
cp "$site/index.html" "$out/site.html"
cp "$site/icon.svg" "$site/icon-maskable.svg" "$site/logo.svg" "$site/apple-touch-icon.png" "$site/manifest.webmanifest" "$out/" 2>/dev/null || true
echo "web root ready at $out (Flutter at /, site at /site.html)"
