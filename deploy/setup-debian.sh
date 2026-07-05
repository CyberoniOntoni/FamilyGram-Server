#!/usr/bin/env bash
# Thin wrapper — use install-lxc.sh for full installation.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec bash "${SCRIPT_DIR}/install-lxc.sh" "$@"