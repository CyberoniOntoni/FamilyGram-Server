#!/usr/bin/env bash
# Testgram installer for Debian 12 LXC/VM (acmechat.example deployment)
#
# Usage (as root):
#   curl -fsSL https://raw.githubusercontent.com/CyberoniOntoni/testgram/dev/deploy/install-lxc.sh -o install-lxc.sh
#   BOT_TOKEN='123:ABC...' bash install-lxc.sh --start
#
# Or from a cloned repo:
#   cd /opt/testgram && bash deploy/install-lxc.sh --start
#
# Options:
#   --start          Pull images and run docker compose up -d (after .env is ready)
#   --bot-token VAL  Set BOT_TOKEN in .env
#   --no-firewall    Skip UFW configuration
#   --help           Show help
#
# LXC (Proxmox): enable nesting + keyctl on the CT, or use a privileged container.
#   pct set <CTID> -features nesting=1,keyctl=1
#
set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/CyberoniOntoni/testgram.git}"
REPO_BRANCH="${REPO_BRANCH:-dev}"
INSTALL_DIR="${INSTALL_DIR:-/opt/testgram}"
COMPOSE_DIR="${INSTALL_DIR}/docker/compose"
PUBLIC_IP="${PUBLIC_IP:-203.0.113.50}"
LAN_IP="${LAN_IP:-192.168.1.79}"

DO_START=false
DO_FIREWALL=true
BOT_TOKEN_ARG=""

log()  { printf '==> %s\n' "$*"; }
warn() { printf '!! %s\n' "$*" >&2; }
die()  { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

usage() {
  sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'
  exit 0
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --start) DO_START=true; shift ;;
    --no-firewall) DO_FIREWALL=false; shift ;;
    --bot-token) BOT_TOKEN_ARG="${2:-}"; shift 2 ;;
    --help|-h) usage ;;
    *) die "Unknown option: $1 (try --help)" ;;
  esac
done

[[ "$(id -u)" -eq 0 ]] || die "Run as root (sudo -i)"

if [[ -r /etc/os-release ]]; then
  # shellcheck source=/dev/null
  . /etc/os-release
  [[ "${ID:-}" == "debian" ]] || warn "This script targets Debian; detected: ${ID:-unknown}"
else
  warn "Cannot detect OS; continuing anyway"
fi

if [[ -f /.dockerenv ]] || grep -q 'lxc\|container' /proc/1/cgroup 2>/dev/null; then
  log "Container environment detected"
  if ! grep -q '^Features:.*nesting' /proc/self/status 2>/dev/null; then
    warn "LXC nesting may be disabled — Docker can fail. On Proxmox host run:"
    warn "  pct set <CTID> -features nesting=1,keyctl=1"
  fi
fi

log "Installing system packages..."
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq ca-certificates curl git gnupg openssl ufw

if ! command -v docker >/dev/null 2>&1; then
  log "Installing Docker..."
  curl -fsSL https://get.docker.com | sh
else
  log "Docker already installed: $(docker --version)"
fi

if ! docker compose version >/dev/null 2>&1; then
  die "docker compose plugin missing — re-run Docker install or install docker-compose-plugin"
fi

log "Cloning or updating Testgram (${REPO_BRANCH})..."
if [[ -d "${INSTALL_DIR}/.git" ]]; then
  git -C "${INSTALL_DIR}" fetch origin
  git -C "${INSTALL_DIR}" checkout "${REPO_BRANCH}"
  git -C "${INSTALL_DIR}" pull --ff-only origin "${REPO_BRANCH}"
else
  git clone --branch "${REPO_BRANCH}" --depth 1 "${REPO_URL}" "${INSTALL_DIR}"
fi

cd "${COMPOSE_DIR}"

ENV_NEW=false
if [[ ! -f .env ]]; then
  log "Creating .env from .env.acmechat.example.example..."
  cp .env.acmechat.example.example .env
  ENV_NEW=true
fi

if [[ -n "${BOT_TOKEN_ARG}" ]]; then
  if grep -q '^BOT_TOKEN=' .env; then
    sed -i "s|^BOT_TOKEN=.*|BOT_TOKEN=${BOT_TOKEN_ARG}|" .env
  else
    echo "BOT_TOKEN=${BOT_TOKEN_ARG}" >> .env
  fi
fi

if [[ "${ENV_NEW}" == true ]] || grep -q 'CHANGE_ME' .env; then
  log "Generating secrets for .env..."
  RABBIT_PW="$(openssl rand -hex 24)"
  MINIO_SK="$(openssl rand -hex 24)"
  ACCESS_HASH="$(openssl rand -hex 32)"
  MSG_KEY="$(openssl rand -base64 32)"
  IDX_KEY="$(openssl rand -base64 32)"

  sed -i "s|^RabbitMQ__Connections__Default__Password=CHANGE_ME|RabbitMQ__Connections__Default__Password=${RABBIT_PW}|" .env
  sed -i "s|^Minio__SecretKey=CHANGE_ME|Minio__SecretKey=${MINIO_SK}|" .env
  sed -i "s|^App__AccessHashSecretKey=CHANGE_ME|App__AccessHashSecretKey=${ACCESS_HASH}|" .env
  sed -i "s|^App__EncryptionConfig__MessageKeys__0__Key=CHANGE_ME|App__EncryptionConfig__MessageKeys__0__Key=${MSG_KEY}|" .env
  sed -i "s|^App__EncryptionConfig__IndexKeys__0__Key=CHANGE_ME|App__EncryptionConfig__IndexKeys__0__Key=${IDX_KEY}|" .env
fi

# Ensure public IP is set (template should already have it)
sed -i "s|^App__DcOptions__0__IpAddress=.*|App__DcOptions__0__IpAddress=${PUBLIC_IP}|" .env
sed -i "s|^App__DcOptions__1__IpAddress=.*|App__DcOptions__1__IpAddress=${PUBLIC_IP}|" .env
sed -i "s|^App__DcOptions__2__IpAddress=.*|App__DcOptions__2__IpAddress=${PUBLIC_IP}|" .env
sed -i "s|^App__DcOptions__3__IpAddress=.*|App__DcOptions__3__IpAddress=${PUBLIC_IP}|" .env
sed -i "s|^App__WebRtcConnections__0__Ip=.*|App__WebRtcConnections__0__Ip=${PUBLIC_IP}|" .env

log "Preparing data directories..."
mkdir -p data/mytelegram data/bot geoip data/redis data/rabbitmq data/mongo/db data/mongo/configdb data/minio data/coturn data/rtmp
chmod -R a+w data

if [[ "${DO_FIREWALL}" == true ]] && command -v ufw >/dev/null 2>&1; then
  log "Configuring UFW..."
  ufw allow 22/tcp comment 'SSH' >/dev/null 2>&1 || true
  ufw allow 20443,20543,20643,20644/tcp comment 'Testgram MTProto' >/dev/null 2>&1 || true
  ufw allow 30443,30444/tcp comment 'Testgram HTTPS' >/dev/null 2>&1 || true
  ufw allow 5348/tcp comment 'Testgram STUN/TURN' >/dev/null 2>&1 || true
  ufw allow 5348/udp comment 'Testgram STUN/TURN' >/dev/null 2>&1 || true
  ufw allow 49152:49172/udp comment 'Testgram TURN relay' >/dev/null 2>&1 || true
  ufw allow 1935/tcp comment 'Testgram RTMP' >/dev/null 2>&1 || true
  ufw allow 8888/tcp comment 'Testgram RTMP HLS' >/dev/null 2>&1 || true
  ufw --force enable
fi

env_ready=true
if grep -q 'CHANGE_ME' .env; then
  warn ".env still contains CHANGE_ME placeholders"
  env_ready=false
fi
if grep -qE '^BOT_TOKEN=(your_bot_token_here)?$' .env || ! grep -q '^BOT_TOKEN=' .env; then
  warn "BOT_TOKEN is not set — login codes will not work until you add a @BotFather token"
  env_ready=false
fi

if [[ "${DO_START}" != true ]]; then
  log "Install prep complete (stack not started)."
  echo ""
  echo "  Config:  ${COMPOSE_DIR}/.env"
  echo "  LAN IP:  ${LAN_IP}  (router forwards to this host)"
  echo "  WAN IP:  ${PUBLIC_IP}  (in DcOptions / WebRTC)"
  echo ""
  echo "  Before starting:"
  echo "    1. Set BOT_TOKEN in .env (from @BotFather)"
  echo "    2. Forward router ports to ${LAN_IP}:"
  echo "       TCP 20443,20543,20643,20644,30443"
  echo "       TCP+UDP 5348, UDP 49152-49172"
  echo "    3. Make GHCR packages public or: docker login ghcr.io"
  echo ""
  echo "  Start stack:"
  echo "    bash ${INSTALL_DIR}/deploy/install-lxc.sh --start"
  echo "    # or: cd ${COMPOSE_DIR} && docker compose up -d"
  exit 0
fi

[[ "${env_ready}" == true ]] || die "Fix .env (BOT_TOKEN / CHANGE_ME) before --start"

log "Pulling Docker images (this may take several minutes)..."
docker compose pull

log "Starting Testgram stack..."
docker compose up -d

log "Waiting for gateway-server (up to 120s)..."
for i in $(seq 1 24); do
  if docker compose logs gateway-server 2>/dev/null | grep -q '20443'; then
    log "Gateway is listening on port 20443"
    break
  fi
  sleep 5
  if [[ "$i" -eq 24 ]]; then
    warn "Gateway did not log 20443 yet — check: docker compose logs gateway-server"
  fi
done

log "Service status:"
docker compose ps

echo ""
echo "Install complete."
echo "  Logs:    cd ${COMPOSE_DIR} && docker compose logs -f"
echo "  Verify:  docker compose logs gateway-server | grep 20443"
echo "  Bot:     open your @BotFather bot → /start → link phone number"
echo "  Guide:   ${INSTALL_DIR}/deploy/DEPLOYMENT-example.md"