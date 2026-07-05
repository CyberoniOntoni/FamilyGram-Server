#!/usr/bin/env bash
# Testgram host bootstrap for Debian 12 on Proxmox.
# Run as root on the VM (192.168.1.79): bash deploy/setup-debian.sh
set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/CyberoniOntoni/testgram.git}"
INSTALL_DIR="${INSTALL_DIR:-/opt/testgram}"
COMPOSE_DIR="${INSTALL_DIR}/docker/compose"

echo "==> Installing Docker..."
if ! command -v docker >/dev/null 2>&1; then
  apt-get update
  apt-get install -y ca-certificates curl git ufw
  curl -fsSL https://get.docker.com | sh
fi

echo "==> Configuring firewall (UFW)..."
ufw allow 22/tcp
ufw allow 20443,20543,20643,20644/tcp
ufw allow 30443,30444/tcp
ufw allow 3478/tcp
ufw allow 3478/udp
ufw allow 49152:49172/udp
ufw allow 1935/tcp
ufw allow 8888/tcp
ufw --force enable

echo "==> Cloning or updating Testgram..."
if [ -d "${INSTALL_DIR}/.git" ]; then
  git -C "${INSTALL_DIR}" pull --ff-only
else
  git clone "${REPO_URL}" "${INSTALL_DIR}"
fi

cd "${COMPOSE_DIR}"

if [ ! -f .env ]; then
  if [ -f .env.acmechat.example.example ]; then
    cp .env.acmechat.example.example .env
    echo "Created .env from .env.acmechat.example.example — edit it before starting."
  else
    cp .env.example .env
    echo "Created .env from .env.example — edit it before starting."
  fi
fi

mkdir -p data/mytelegram data/bot geoip
chmod -R a+w data

echo ""
echo "Next steps:"
echo "  1. Edit ${COMPOSE_DIR}/.env"
echo "     - Set YOUR_PUBLIC_IP to your WAN address (tg.acmechat.example DNS target)"
echo "     - Set strong passwords and BOT_TOKEN from @BotFather"
echo "  2. Forward router ports 20443,20543,20643,20644,3478,49152-49172 to 192.168.1.79"
echo "  3. Cloudflare DNS-only A record: tg.acmechat.example -> YOUR_PUBLIC_IP"
echo "  4. docker compose pull && docker compose up -d"
echo "  5. Verify: docker compose logs gateway-server | grep 20443"