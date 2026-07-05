#!/usr/bin/env bash
# Testgram — interactive Docker Compose installer
#
# Usage (save first, then run — do NOT curl | bash):
#   curl -fsSL https://raw.githubusercontent.com/CyberoniOntoni/testgram/dev/deploy/install.sh -o install.sh
#   sudo bash install.sh
#
# Non-interactive:
#   PUBLIC_IP=1.2.3.4 LAN_IP=192.168.1.10 BOT_TOKEN='123:ABC' \
#     sudo bash install.sh --non-interactive --start
#
# Options:
#   --start              docker compose pull && up -d after setup
#   --non-interactive    skip prompts (provide env vars)
#   --no-firewall        never configure UFW
#   --no-docker-install  fail if docker missing instead of installing
#   --public-ip IP       set PUBLIC_IP
#   --lan-ip IP          set LAN_IP
#   --brand NAME         set App__Brand
#   --passkey-domain D   set passkey domain
#   --bot-token TOKEN    set BOT_TOKEN
#   --install-dir PATH   default /opt/testgram
#   --help               show help
set -euo pipefail

INSTALLER_VERSION="3.1.0"

REPO_URL="${REPO_URL:-https://github.com/CyberoniOntoni/testgram.git}"
REPO_BRANCH="${REPO_BRANCH:-dev}"
INSTALL_DIR="${INSTALL_DIR:-/opt/testgram}"
COMPOSE_DIR="${INSTALL_DIR}/docker/compose"
COMPOSE_FILE="${COMPOSE_DIR}/docker-compose.yml"

DO_START=false
DO_FIREWALL=true
NON_INTERACTIVE=false
INSTALL_DOCKER="${INSTALL_DOCKER:-}"
CUSTOMIZE_PORTS="${CUSTOMIZE_PORTS:-}"

PUBLIC_IP="${PUBLIC_IP:-}"
LAN_IP="${LAN_IP:-}"
BRAND="${BRAND:-}"
PASSKEY_DOMAIN="${PASSKEY_DOMAIN:-}"
BOT_TOKEN="${BOT_TOKEN:-}"
ENABLE_PASSKEY="${ENABLE_PASSKEY:-}"
ENABLE_RTMP="${ENABLE_RTMP:-}"

PORT_MT1="${PORT_MT1:-20443}"
PORT_MT2="${PORT_MT2:-20543}"
PORT_MT3="${PORT_MT3:-20643}"
PORT_MT4="${PORT_MT4:-20644}"
PORT_HTTPS="${PORT_HTTPS:-30443}"
PORT_HTTPS_ALT="${PORT_HTTPS_ALT:-30444}"
PORT_STUN="${PORT_STUN:-5348}"
PORT_RELAY_MIN="${PORT_RELAY_MIN:-49152}"
PORT_RELAY_MAX="${PORT_RELAY_MAX:-49172}"
PORT_RTMP="${PORT_RTMP:-1935}"
PORT_RTMP_HLS="${PORT_RTMP_HLS:-8888}"

TURN_USER="${TURN_USER:-testgram}"
TURN_PASS="${TURN_PASS:-}"
SUMMARY_FILE=""

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

resolve_installer_lib() {
  local path
  local candidates=(
    "${SCRIPT_DIR}/lib/installer-lib.sh"
    "${LOCAL_REPO_ROOT}/deploy/lib/installer-lib.sh"
  )
  for path in "${candidates[@]}"; do
    if [[ -f "${path}" ]]; then
      printf '%s\n' "${path}"
      return 0
    fi
  done

  # Standalone curl download (e.g. /root/install.sh) — fetch lib from GitHub
  local cache_dir="/tmp/testgram-installer-${INSTALLER_VERSION}"
  local cached="${cache_dir}/installer-lib.sh"
  local raw_url="https://raw.githubusercontent.com/CyberoniOntoni/testgram/${REPO_BRANCH}/deploy/lib/installer-lib.sh"
  mkdir -p "${cache_dir}"
  if command -v curl >/dev/null 2>&1; then
    curl -fsSL "${raw_url}" -o "${cached}"
  elif command -v wget >/dev/null 2>&1; then
    wget -qO "${cached}" "${raw_url}"
  else
    printf 'ERROR: installer-lib.sh not found next to install.sh and curl/wget unavailable\n' >&2
    printf 'Clone the repo instead: git clone -b %s %s\n' "${REPO_BRANCH}" "${REPO_URL}" >&2
    return 1
  fi
  [[ -s "${cached}" ]] || return 1
  printf '%s\n' "${cached}"
}

INSTALLER_LIB="$(resolve_installer_lib)" || exit 1
# shellcheck source=lib/installer-lib.sh
source "${INSTALLER_LIB}"

usage() {
  sed -n '2,24p' "$0" | sed 's/^# \{0,1\}//'
  echo ""
  echo "Environment variables (for --non-interactive):"
  echo "  PUBLIC_IP, LAN_IP, BRAND, PASSKEY_DOMAIN, BOT_TOKEN"
  echo "  ENABLE_PASSKEY=yes|no, ENABLE_RTMP=yes|no, CUSTOMIZE_PORTS=yes|no"
  echo "  INSTALL_DOCKER=yes|no, DO_FIREWALL=yes|no"
  echo "  PORT_MT1..PORT_MT4, PORT_HTTPS, PORT_STUN, PORT_RELAY_MIN, PORT_RELAY_MAX"
  echo "  TURN_USER, TURN_PASS, INSTALL_DIR, REPO_BRANCH"
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --start) DO_START=true; shift ;;
    --non-interactive) NON_INTERACTIVE=true; shift ;;
    --no-firewall) DO_FIREWALL=false; shift ;;
    --no-docker-install) INSTALL_DOCKER=no; shift ;;
    --public-ip) PUBLIC_IP="${2:-}"; shift 2 ;;
    --lan-ip) LAN_IP="${2:-}"; shift 2 ;;
    --brand) BRAND="${2:-}"; shift 2 ;;
    --passkey-domain) PASSKEY_DOMAIN="${2:-}"; shift 2 ;;
    --bot-token) BOT_TOKEN="${2:-}"; shift 2 ;;
    --install-dir) INSTALL_DIR="${2:-}"; COMPOSE_DIR="${INSTALL_DIR}/docker/compose"; COMPOSE_FILE="${COMPOSE_DIR}/docker-compose.yml"; shift 2 ;;
    --help|-h) usage ;;
    *) die "Unknown option: $1 (try --help)" ;;
  esac
done

installer_lib_init

if [[ -f "${LOCAL_REPO_ROOT}/docker/compose/.env.example" ]]; then
  INSTALL_DIR="${INSTALL_DIR:-${LOCAL_REPO_ROOT}}"
  COMPOSE_DIR="${INSTALL_DIR}/docker/compose"
  COMPOSE_FILE="${COMPOSE_DIR}/docker-compose.yml"
fi

maybe_self_update "$@"
setup_interactive_stdin

[[ "$(id -u)" -eq 0 ]] || die "Run as root: sudo bash install.sh"

if [[ "${NON_INTERACTIVE}" == true ]]; then
  CUSTOMIZE_PORTS="${CUSTOMIZE_PORTS:-no}"
  ENABLE_PASSKEY="${ENABLE_PASSKEY:-no}"
  ENABLE_RTMP="${ENABLE_RTMP:-no}"
  INSTALL_DOCKER="${INSTALL_DOCKER:-yes}"
  [[ -n "${INSTALL_DOCKER}" ]] || INSTALL_DOCKER=yes
  [[ -n "${PUBLIC_IP}" ]] || die "PUBLIC_IP required in non-interactive mode"
  if [[ -z "${LAN_IP}" ]]; then
    LAN_IP="$(detect_lan_ip 2>/dev/null || true)"
  fi
  if [[ -z "${PUBLIC_IP}" ]]; then
    PUBLIC_IP="$(detect_public_ip 2>/dev/null || true)"
  fi
  [[ -n "${LAN_IP}" ]] || die "LAN_IP required in non-interactive mode"
  [[ -n "${PUBLIC_IP}" ]] || die "PUBLIC_IP required in non-interactive mode"
  [[ -n "${BRAND}" ]] || BRAND="Testgram"
  [[ -n "${BOT_TOKEN}" ]] || die "BOT_TOKEN required in non-interactive mode"
  if [[ "${ENABLE_PASSKEY}" == "yes" ]]; then
    [[ -n "${PASSKEY_DOMAIN}" ]] || die "PASSKEY_DOMAIN required when ENABLE_PASSKEY=yes"
  else
    PASSKEY_DOMAIN="${PASSKEY_DOMAIN:-localhost}"
  fi
  TURN_PASS="${TURN_PASS:-$(openssl rand -hex 16)}"
  PUBLIC_IP="$(sanitize_ip_input "${PUBLIC_IP}")"
  LAN_IP="$(sanitize_ip_input "${LAN_IP}")"
  is_ipv4 "${PUBLIC_IP}" || die "Invalid PUBLIC_IP: ${PUBLIC_IP}"
  is_ipv4 "${LAN_IP}" || die "Invalid LAN_IP: ${LAN_IP}"
  run_install_apply
else
  run_install_wizard
  run_install_apply
fi