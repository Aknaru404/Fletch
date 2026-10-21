#!/bin/bash
# Installs the .NET 10 SDK and restores the test project so G1 can run in cloud sessions.
set -euo pipefail

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

DOTNET_DIR="$HOME/.dotnet"

if [ ! -x "$DOTNET_DIR/dotnet" ] || ! "$DOTNET_DIR/dotnet" --list-sdks | grep -q '^10\.'; then
  installer="$(mktemp)"
  curl -sSL https://dot.net/v1/dotnet-install.sh -o "$installer"
  bash "$installer" --channel 10.0 --install-dir "$DOTNET_DIR"
  rm -f "$installer"
fi

if [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  {
    echo "export DOTNET_ROOT=\"$DOTNET_DIR\""
    echo "export PATH=\"$DOTNET_DIR:\$PATH\""
    echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
    echo "export DOTNET_NOLOGO=1"
  } >> "$CLAUDE_ENV_FILE"
fi

# Android and iOS heads need workloads this container cannot install; restore only what G1 builds.
"$DOTNET_DIR/dotnet" restore "$CLAUDE_PROJECT_DIR/QuiverLauncher.Tests/QuiverLauncher.Tests.csproj" --verbosity quiet
