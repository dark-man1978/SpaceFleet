#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "$0")"
GODOT_BIN="${GODOT_BIN:-godot}"
dotnet build SpaceFleet.csproj
"$GODOT_BIN" --headless --path . --editor --import --quit
mkdir -p build
"$GODOT_BIN" --headless --path . --export-release "Steam Deck"
