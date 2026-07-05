#!/usr/bin/env bash
# Debian host wrapper — runs the Docker interactive installer.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec bash "${SCRIPT_DIR}/install.sh" "$@"