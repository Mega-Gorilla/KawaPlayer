#!/bin/sh
# Renders poster.html to ../kawaplayer-quickstart-poster.png (1448 x 2048)
# with headless Microsoft Edge. Run from Git Bash on Windows:
#   sh "Media~/poster/source/render.sh"
# The headings use Zen Maru Gothic from Google Fonts, so it needs the network.
set -e
cd "$(dirname "$0")"
D=$(cygpath -m "$PWD")
EDGE=${EDGE:-"/c/Program Files (x86)/Microsoft/Edge/Application/msedge.exe"}
PROFILE=$(mktemp -d)
"$EDGE" --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 \
  --allow-file-access-from-files --user-data-dir="$(cygpath -m "$PROFILE")" \
  --window-size=1448,2048 --virtual-time-budget=4000 \
  --screenshot="$D/../kawaplayer-quickstart-poster.png" "file:///$D/poster.html"
# Edge can still hold its profile for a moment; a profile left in the temp folder is harmless.
rm -rf "$PROFILE" 2>/dev/null || true
