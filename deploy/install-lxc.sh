#!/usr/bin/env bash
# Proxmox LXC wrapper — runs the Docker interactive installer with LXC checks.
# Prefer deploy/install.sh for new deployments.
set -euo pipefail
export INSTALLER_PROFILE=lxc
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec bash "${SCRIPT_DIR}/install.sh" "$@"