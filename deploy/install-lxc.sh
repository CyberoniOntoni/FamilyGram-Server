#!/usr/bin/env bash
# Testgram interactive installer for Debian 12 LXC/VM
#
# Run as root:
#   curl -fsSL https://raw.githubusercontent.com/CyberoniOntoni/testgram/dev/deploy/install-lxc.sh -o install-lxc.sh
#   bash install-lxc.sh
#
# Non-interactive:
#   PUBLIC_IP=1.2.3.4 LAN_IP=192.168.1.10 BOT_TOKEN='123:ABC' bash install-lxc.sh --non-interactive --start
#
# Options:
#   --start              Pull images and start docker compose after setup
#   --non-interactive    Skip prompts (set vars via env or flags)
#   --no-firewall        Skip UFW configuration
#   --help               Show help
#
# LXC (Proxmox): pct set <CTID> -features nesting=1,keyctl=1
#
# IMPORTANT: save the script first, then run it. Do NOT use: curl ... | bash
set -euo pipefail

INSTALLER_VERSION="2.1.0"

REPO_URL="${REPO_URL:-https://github.com/CyberoniOntoni/testgram.git}"
REPO_BRANCH="${REPO_BRANCH:-dev}"
INSTALL_DIR="${INSTALL_DIR:-/opt/testgram}"
COMPOSE_DIR="${INSTALL_DIR}/docker/compose"
COMPOSE_FILE="${COMPOSE_DIR}/docker-compose.yml"

DO_START=false
DO_FIREWALL=true
NON_INTERACTIVE=false

# Config (filled by wizard or env/flags)
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

# ── UI helpers ────────────────────────────────────────────────────────────────

if [[ -t 1 ]]; then
  C_RESET='\033[0m'
  C_BOLD='\033[1m'
  C_DIM='\033[2m'
  C_GREEN='\033[32m'
  C_YELLOW='\033[33m'
  C_CYAN='\033[36m'
  C_RED='\033[31m'
else
  C_RESET='' C_BOLD='' C_DIM='' C_GREEN='' C_YELLOW='' C_CYAN='' C_RED=''
fi

hr()  { printf '%s\n' "────────────────────────────────────────────────────────────"; }
log() { printf '%s==>%s %s\n' "${C_GREEN}" "${C_RESET}" "$*"; }
warn() { printf '%s!!%s %s\n' "${C_YELLOW}" "${C_RESET}" "$*" >&2; }
die()  { printf '%sERROR:%s %s\n' "${C_RED}" "${C_RESET}" "$*" >&2; exit 1; }

banner() {
  printf '\n%s%s'
  cat <<'EOF'
████████╗███████╗███████╗████████╗ ██████╗ ██████╗  █████╗ ███╗   ███╗
╚══██╔══╝██╔════╝██╔════╝╚══██╔══╝██╔════╝ ██╔══██╗██╔══██╗████╗ ████║
   ██║   █████╗  ███████╗   ██║   ██║  ███╗██████╔╝███████║██╔████╔██║
   ██║   ██╔══╝  ╚════██║   ██║   ██║   ██║██╔══██╗██╔══██║██║╚██╔╝██║
   ██║   ███████╗███████║   ██║   ╚██████╔╝██║  ██║██║  ██║██║ ╚═╝ ██║
   ╚═╝   ╚══════╝╚══════╝   ╚═╝    ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝     ╚═╝
EOF
  printf '%s\n' "${C_RESET}"
  printf '  %sSelf-hosted Telegram-compatible server — Debian LXC/VM installer%s\n\n' "${C_BOLD}" "${C_RESET}"
}

step() {
  printf '\n%s%s Step %d/%d — %s%s\n' "${C_CYAN}${C_BOLD}" "${C_RESET}" "$1" "$2" "$3" "${C_RESET}"
  hr
}

usage() {
  sed -n '2,18p' "$0" | sed 's/^# \{0,1\}//'
  echo ""
  echo "Environment variables (for --non-interactive):"
  echo "  PUBLIC_IP, LAN_IP, BRAND, PASSKEY_DOMAIN, BOT_TOKEN"
  echo "  ENABLE_PASSKEY=yes|no, ENABLE_RTMP=yes|no"
  echo "  PORT_MT1..PORT_MT4, PORT_HTTPS, PORT_STUN, PORT_RELAY_MIN, PORT_RELAY_MAX"
  echo "  TURN_USER, TURN_PASS, INSTALL_DIR, REPO_BRANCH"
  exit 0
}

is_ipv4() {
  local ip="$1"
  [[ "$ip" =~ ^([0-9]{1,3}\.){3}[0-9]{1,3}$ ]] || return 1
  local o
  IFS='.' read -r -a o <<< "$ip"
  for x in "${o[@]}"; do
    [[ "$x" -le 255 ]] || return 1
  done
}

is_domain() {
  [[ "$1" =~ ^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$ ]]
}

is_port() {
  [[ "$1" =~ ^[0-9]+$ ]] && (( "$1" >= 1 && "$1" <= 65535 ))
}

detect_lan_ip() {
  ip -4 route get 1.1.1.1 2>/dev/null | awk '{for (i=1;i<=NF;i++) if ($i=="src") {print $(i+1); exit}}' || true
}

# Read from the real terminal — required when stdin is a pipe or redirected.
setup_interactive_stdin() {
  if [[ "${NON_INTERACTIVE}" == true ]]; then
    return 0
  fi
  if [[ -t 0 ]]; then
    return 0
  fi
  if [[ -r /dev/tty ]]; then
    exec </dev/tty
    return 0
  fi
  die "No interactive terminal.

Do NOT pipe this script (curl ... | bash) — that steals stdin and skips prompts.

Instead:
  curl -fsSL https://raw.githubusercontent.com/CyberoniOntoni/testgram/dev/deploy/install-lxc.sh -o install-lxc.sh
  bash install-lxc.sh

Or pass all values explicitly:
  PUBLIC_IP=... LAN_IP=... BOT_TOKEN=... bash install-lxc.sh --non-interactive"
}

read_line() {
  local __var="$1"
  local __line=""
  if ! IFS= read -r __line; then
    die "Could not read input. Use an SSH session with a TTY (ssh -t root@host)."
  fi
  __line="${__line//$'\r'/}"
  printf -v "$__var" '%s' "$__line"
}

maybe_self_update() {
  local repo_script="${LOCAL_REPO_ROOT}/deploy/install-lxc.sh"
  [[ -f "${repo_script}" ]] || return 0
  [[ -d "${LOCAL_REPO_ROOT}/.git" ]] || return 0
  [[ "${INSTALLER_SELF_UPDATED:-}" == "1" ]] && return 0

  log "Updating installer from git (${REPO_BRANCH})..."
  git -C "${LOCAL_REPO_ROOT}" fetch origin "${REPO_BRANCH}" 2>/dev/null || true
  git -C "${LOCAL_REPO_ROOT}" checkout "${REPO_BRANCH}" 2>/dev/null || true
  git -C "${LOCAL_REPO_ROOT}" pull --ff-only origin "${REPO_BRANCH}" 2>/dev/null || true

  export INSTALLER_SELF_UPDATED=1
  exec bash "${repo_script}" "$@"
}

prompt() {
  local var_name="$1" prompt_text="$2" default="${3:-}" validate="${4:-}"
  local input="" display_default=""

  if [[ "${NON_INTERACTIVE}" == true ]]; then
    if [[ -z "${!var_name:-}" ]]; then
      if [[ -n "$default" ]]; then
        printf -v "$var_name" '%s' "$default"
      else
        die "Missing required value for ${var_name} in non-interactive mode"
      fi
    fi
    return 0
  fi

  if [[ -n "$default" ]]; then
    display_default=" ${C_DIM}[${default}]${C_RESET}"
  fi

  while true; do
    printf '  %s%s: ' "$prompt_text" "$display_default" >&2
    read_line input
    input="${input:-$default}"
    if [[ -z "$input" ]]; then
      warn "This field is required."
      continue
    fi
    if [[ -n "$validate" ]] && ! "$validate" "$input"; then
      warn "Invalid value — try again."
      continue
    fi
    printf -v "$var_name" '%s' "$input"
    break
  done
}

prompt_yes_no() {
  local var_name="$1" prompt_text="$2" default="${3:-yes}"
  local input="" hint="Y/n"

  if [[ "${NON_INTERACTIVE}" == true ]]; then
    [[ -n "${!var_name:-}" ]] || printf -v "$var_name" '%s' "$default"
    return 0
  fi

  [[ "$default" == "no" ]] && hint="y/N"

  while true; do
    printf '  %s %s(%s): ' "$prompt_text" "${C_DIM}" "$hint" >&2
    read_line input
    input="${input:-$default}"
    case "${input,,}" in
      y|yes)  printf -v "$var_name" '%s' "yes"; break ;;
      n|no)   printf -v "$var_name" '%s' "no"; break ;;
      *) warn "Answer y or n." ;;
    esac
  done
}

confirm() {
  local prompt_text="$1" default="${2:-yes}"
  if [[ "${NON_INTERACTIVE}" == true ]]; then
    return 0
  fi
  local input=""
  printf '  %s (Y/n): ' "$prompt_text" >&2
  read_line input
  input="${input:-$default}"
  [[ "${input,,}" == "y" || "${input,,}" == "yes" ]]
}

# ── Argument parsing ──────────────────────────────────────────────────────────

while [[ $# -gt 0 ]]; do
  case "$1" in
    --start) DO_START=true; shift ;;
    --non-interactive) NON_INTERACTIVE=true; shift ;;
    --no-firewall) DO_FIREWALL=false; shift ;;
    --public-ip) PUBLIC_IP="${2:-}"; shift 2 ;;
    --lan-ip) LAN_IP="${2:-}"; shift 2 ;;
    --brand) BRAND="${2:-}"; shift 2 ;;
    --passkey-domain) PASSKEY_DOMAIN="${2:-}"; shift 2 ;;
    --bot-token) BOT_TOKEN="${2:-}"; shift 2 ;;
    --help|-h) usage ;;
    *) die "Unknown option: $1 (try --help)" ;;
  esac
done

[[ "$(id -u)" -eq 0 ]] || die "Run as root: sudo -i && bash install-lxc.sh"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
LOCAL_REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
if [[ -f "${LOCAL_REPO_ROOT}/docker/compose/.env.example" ]]; then
  INSTALL_DIR="${INSTALL_DIR:-${LOCAL_REPO_ROOT}}"
  COMPOSE_DIR="${INSTALL_DIR}/docker/compose"
  COMPOSE_FILE="${COMPOSE_DIR}/docker-compose.yml"
fi

maybe_self_update "$@"
setup_interactive_stdin

# ── Wizard ────────────────────────────────────────────────────────────────────

banner
printf '  %sInstaller v%s — interactive mode%s\n\n' "${C_DIM}" "${INSTALLER_VERSION}" "${C_RESET}"

TOTAL_STEPS=6

step 1 "$TOTAL_STEPS" "Welcome & prerequisites"
printf '%s\n' \
  "This script will:" \
  "  • Install Docker and dependencies" \
  "  • Clone Testgram and generate a secure .env" \
  "  • Configure the firewall (UFW)" \
  "  • Optionally start the full stack" \
  ""
printf '%sImportant:%s\n' "${C_YELLOW}" "${C_RESET}"
printf '%s\n' \
  "  • Use your **public WAN IP** for client connections (not your LAN IP)" \
  "  • MTProto must NOT go through Cloudflare proxy or reverse proxies" \
  "  • You need a @BotFather token for login verification codes" \
  ""

if [[ -f /.dockerenv ]] || grep -qE 'lxc|container' /proc/1/cgroup 2>/dev/null; then
  log "Container environment detected"
  if ! grep -q '^Features:.*nesting' /proc/self/status 2>/dev/null; then
    warn "LXC nesting may be disabled — Docker can fail."
    warn "On the Proxmox host: pct set <CTID> -features nesting=1,keyctl=1"
    warn "Then restart the container."
  fi
else
  log "Bare-metal or VM environment detected"
fi

if [[ -r /etc/os-release ]]; then
  # shellcheck source=/dev/null
  . /etc/os-release
  log "OS: ${PRETTY_NAME:-unknown}"
  [[ "${ID:-}" == "debian" ]] || warn "This script targets Debian; you may hit package issues."
fi

if ! confirm "Continue with installation?"; then
  printf '\nAborted.\n'
  exit 0
fi

step 2 "$TOTAL_STEPS" "Network & branding"
printf '%s\n' \
  "Clients connect using your public IP. The LAN IP is only used for router port forwards." \
  ""

detected_lan="$(detect_lan_ip)"
prompt PUBLIC_IP "Public WAN IP (what the internet sees)" "" is_ipv4
prompt LAN_IP "LAN IP of this host (router forwards to)" "${detected_lan:-}" is_ipv4

printf '\n'
prompt BRAND "App / brand name (shown in welcome messages)" "Testgram"
prompt_yes_no ENABLE_PASSKEY "Enable passkey (WebAuthn) login? Requires HTTPS + domain" "no"

if [[ "${ENABLE_PASSKEY}" == "yes" ]]; then
  prompt PASSKEY_DOMAIN "Passkey domain (e.g. tg.example.com — no https://)" "" is_domain
else
  PASSKEY_DOMAIN="${PASSKEY_DOMAIN:-localhost}"
fi

step 3 "$TOTAL_STEPS" "Service ports"
printf '%s\n' \
  "Defaults match the upstream Testgram stack. Change only if you know you need to." \
  "STUN/TURN defaults to 5348 (avoids Microsoft Teams range 3478–3481)." \
  ""

prompt PORT_MT1 "MTProto DC1 port (main client entry)" "$PORT_MT1" is_port
prompt PORT_MT2 "MTProto port 2" "$PORT_MT2" is_port
prompt PORT_MT3 "MTProto port 3" "$PORT_MT3" is_port
prompt PORT_MT4 "MTProto port 4 (media DC)" "$PORT_MT4" is_port
prompt PORT_HTTPS "HTTPS / web port (passkey, web client)" "$PORT_HTTPS" is_port
prompt PORT_STUN "STUN/TURN port (TCP + UDP)" "$PORT_STUN" is_port
prompt PORT_RELAY_MIN "TURN relay UDP range — start" "$PORT_RELAY_MIN" is_port
prompt PORT_RELAY_MAX "TURN relay UDP range — end" "$PORT_RELAY_MAX" is_port

if (( PORT_RELAY_MIN >= PORT_RELAY_MAX )); then
  die "Relay range invalid: min (${PORT_RELAY_MIN}) must be less than max (${PORT_RELAY_MAX})"
fi

prompt_yes_no ENABLE_RTMP "Enable RTMP live streaming ports?" "no"
if [[ "${ENABLE_RTMP}" == "yes" ]]; then
  prompt PORT_RTMP "RTMP port" "$PORT_RTMP" is_port
  prompt PORT_RTMP_HLS "RTMP HLS port" "$PORT_RTMP_HLS" is_port
fi

step 4 "$TOTAL_STEPS" "Telegram bot & WebRTC credentials"
printf '%s\n' \
  "Create a bot via @BotFather on Telegram and paste the token below." \
  "Users link their phone number to this bot to receive login codes." \
  ""

if [[ -n "${BOT_TOKEN}" ]]; then
  log "BOT_TOKEN provided via environment/flag"
else
  while true; do
    printf '  Bot token from @BotFather: ' >&2
    read_line BOT_TOKEN
    if [[ "$BOT_TOKEN" =~ ^[0-9]+:[A-Za-z0-9_-]+$ ]]; then
      break
    fi
    warn "Token should look like 123456789:AAH..."
  done
fi

TURN_PASS="${TURN_PASS:-$(openssl rand -hex 16)}"
log "Generated TURN password (saved in .env)"

step 5 "$TOTAL_STEPS" "Review configuration"
hr
printf '  %-22s %s\n' "Install directory:" "${INSTALL_DIR}"
printf '  %-22s %s\n' "Repository branch:" "${REPO_BRANCH}"
printf '  %-22s %s\n' "Public WAN IP:" "${PUBLIC_IP}"
printf '  %-22s %s\n' "LAN IP (for router):" "${LAN_IP}"
printf '  %-22s %s\n' "Brand:" "${BRAND}"
printf '  %-22s %s\n' "Passkey:" "${ENABLE_PASSKEY} (${PASSKEY_DOMAIN})"
printf '  %-22s %s\n' "MTProto ports:" "${PORT_MT1}, ${PORT_MT2}, ${PORT_MT3}, ${PORT_MT4}"
printf '  %-22s %s\n' "HTTPS port:" "${PORT_HTTPS}"
printf '  %-22s %s\n' "STUN/TURN:" "${PORT_STUN} (TCP+UDP)"
printf '  %-22s %s\n' "TURN relay:" "${PORT_RELAY_MIN}-${PORT_RELAY_MAX} (UDP)"
if [[ "${ENABLE_RTMP}" == "yes" ]]; then
  printf '  %-22s %s\n' "RTMP:" "${PORT_RTMP}, HLS ${PORT_RTMP_HLS}"
fi
printf '  %-22s %s\n' "Bot token:" "${BOT_TOKEN:0:12}..."
hr

if ! confirm "Proceed with install using these settings?"; then
  printf '\nAborted. No changes made.\n'
  exit 0
fi

# ── Install system packages & Docker ─────────────────────────────────────────

step 6 "$TOTAL_STEPS" "Installing software"

export DEBIAN_FRONTEND=noninteractive
log "Updating apt..."
apt-get update -qq
apt-get install -y -qq ca-certificates curl git gnupg openssl ufw iproute2

if ! command -v docker >/dev/null 2>&1; then
  log "Installing Docker (official script)..."
  curl -fsSL https://get.docker.com | sh
else
  log "Docker already installed: $(docker --version)"
fi

docker compose version >/dev/null 2>&1 || die "docker compose plugin missing"

log "Cloning or updating Testgram (${REPO_BRANCH})..."
if [[ -d "${INSTALL_DIR}/.git" ]]; then
  git -C "${INSTALL_DIR}" fetch origin
  git -C "${INSTALL_DIR}" checkout "${REPO_BRANCH}"
  git -C "${INSTALL_DIR}" pull --ff-only origin "${REPO_BRANCH}" || warn "git pull failed — using existing checkout"
else
  git clone --branch "${REPO_BRANCH}" --depth 1 "${REPO_URL}" "${INSTALL_DIR}"
fi

cd "${COMPOSE_DIR}"

# ── Generate .env ─────────────────────────────────────────────────────────────

log "Writing ${COMPOSE_DIR}/.env ..."
cp .env.example .env

RABBIT_PW="$(openssl rand -hex 24)"
MINIO_SK="$(openssl rand -hex 24)"
ACCESS_HASH="$(openssl rand -hex 32)"
MSG_KEY="$(openssl rand -base64 32)"
IDX_KEY="$(openssl rand -base64 32)"

set_env() {
  local key="$1" val="$2"
  if grep -q "^${key}=" .env; then
    sed -i "s|^${key}=.*|${key}=${val}|" .env
  else
    echo "${key}=${val}" >> .env
  fi
}

set_env RabbitMQ__Connections__Default__Password "$RABBIT_PW"
set_env Minio__SecretKey "$MINIO_SK"
set_env App__AccessHashSecretKey "$ACCESS_HASH"
set_env App__EncryptionConfig__MessageKeys__0__Key "$MSG_KEY"
set_env App__EncryptionConfig__IndexKeys__0__Key "$IDX_KEY"

set_env App__Brand "$BRAND"
set_env "App__WelcomeMsg" "Welcome to ${BRAND}! Your account has been created."
set_env App__SendWelcomeMessageAfterUserSignIn "True"
set_env App__PasskeyRpId "$PASSKEY_DOMAIN"
set_env App__PasskeyRpName "$BRAND"
set_env BOT_TOKEN "$BOT_TOKEN"

set_env App__Servers__0__Port "$PORT_MT1"
set_env App__Servers__1__Port "$PORT_MT2"
set_env App__Servers__2__Port "$PORT_MT3"
set_env App__Servers__3__Port "$PORT_MT4"
set_env App__Servers__4__Port "$PORT_HTTPS"
set_env App__Servers__5__Port "$PORT_HTTPS_ALT"

set_env App__WebRtcConnections__0__Ip "$PUBLIC_IP"
set_env App__WebRtcConnections__0__Port "$PORT_STUN"
set_env App__WebRtcConnections__0__UserName "$TURN_USER"
set_env App__WebRtcConnections__0__Password "$TURN_PASS"

for i in 0 1 2 3; do
  set_env "App__DcOptions__${i}__IpAddress" "$PUBLIC_IP"
done
set_env App__DcOptions__0__Port "$PORT_MT1"
set_env App__DcOptions__1__Port "$PORT_MT2"
set_env App__DcOptions__2__Port "$PORT_MT3"
set_env App__DcOptions__3__Port "$PORT_MT4"

set_env RTMP_PORT "$PORT_RTMP"
set_env RTMP_HLS_PORT "$PORT_RTMP_HLS"

# ── Patch docker-compose for Coturn / RTMP ports ──────────────────────────────

patch_compose() {
  local file="$1"
  [[ -f "$file" ]] || die "Missing ${file}"

  # Coturn published ports and command-line args
  sed -i \
    -e "s|\"5348:5348\"|\"${PORT_STUN}:${PORT_STUN}\"|g" \
    -e "s|\"5348:5348/udp\"|\"${PORT_STUN}:${PORT_STUN}/udp\"|g" \
    -e "s|49152-49172:49152-49172|${PORT_RELAY_MIN}-${PORT_RELAY_MAX}:${PORT_RELAY_MIN}-${PORT_RELAY_MAX}|g" \
    -e "s|--listening-port 5348|--listening-port ${PORT_STUN}|g" \
    -e "s|--min-port 49152|--min-port ${PORT_RELAY_MIN}|g" \
    -e "s|--max-port 49172|--max-port ${PORT_RELAY_MAX}|g" \
    -e "s|--user testgram:testgram2024|--user ${TURN_USER}:${TURN_PASS}|g" \
    "$file"
}

patch_compose "${COMPOSE_FILE}"

log "Preparing data directories..."
mkdir -p data/mytelegram data/bot geoip data/redis data/rabbitmq \
  data/mongo/db data/mongo/configdb data/minio data/coturn data/rtmp
chmod -R a+w data

# Save install summary for later reference
SUMMARY_FILE="${INSTALL_DIR}/deploy/last-install-config.txt"
mkdir -p "${INSTALL_DIR}/deploy"
cat > "${SUMMARY_FILE}" <<EOF
# Testgram install config — $(date -Iseconds)
PUBLIC_IP=${PUBLIC_IP}
LAN_IP=${LAN_IP}
BRAND=${BRAND}
PASSKEY_DOMAIN=${PASSKEY_DOMAIN}
ENABLE_PASSKEY=${ENABLE_PASSKEY}
ENABLE_RTMP=${ENABLE_RTMP}
PORT_MT1=${PORT_MT1}
PORT_MT2=${PORT_MT2}
PORT_MT3=${PORT_MT3}
PORT_MT4=${PORT_MT4}
PORT_HTTPS=${PORT_HTTPS}
PORT_STUN=${PORT_STUN}
PORT_RELAY_MIN=${PORT_RELAY_MIN}
PORT_RELAY_MAX=${PORT_RELAY_MAX}
PORT_RTMP=${PORT_RTMP}
PORT_RTMP_HLS=${PORT_RTMP_HLS}
TURN_USER=${TURN_USER}
COMPOSE_DIR=${COMPOSE_DIR}
EOF

# ── Firewall ──────────────────────────────────────────────────────────────────

if [[ "${DO_FIREWALL}" == true ]] && command -v ufw >/dev/null 2>&1; then
  log "Configuring UFW..."
  ufw allow 22/tcp comment 'SSH' >/dev/null 2>&1 || true
  ufw allow "${PORT_MT1},${PORT_MT2},${PORT_MT3},${PORT_MT4}/tcp" comment 'Testgram MTProto' >/dev/null 2>&1 || true
  ufw allow "${PORT_HTTPS},${PORT_HTTPS_ALT}/tcp" comment 'Testgram HTTPS' >/dev/null 2>&1 || true
  ufw allow "${PORT_STUN}/tcp" comment 'Testgram STUN/TURN' >/dev/null 2>&1 || true
  ufw allow "${PORT_STUN}/udp" comment 'Testgram STUN/TURN' >/dev/null 2>&1 || true
  ufw allow "${PORT_RELAY_MIN}:${PORT_RELAY_MAX}/udp" comment 'Testgram TURN relay' >/dev/null 2>&1 || true
  if [[ "${ENABLE_RTMP}" == "yes" ]]; then
    ufw allow "${PORT_RTMP}/tcp" comment 'Testgram RTMP' >/dev/null 2>&1 || true
    ufw allow "${PORT_RTMP_HLS}/tcp" comment 'Testgram RTMP HLS' >/dev/null 2>&1 || true
  fi
  ufw --force enable
fi

# ── Port forward summary ──────────────────────────────────────────────────────

print_port_forwards() {
  printf '\n%s%sRouter port forwards%s\n' "${C_BOLD}" "${C_CYAN}" "${C_RESET}"
  printf '%s\n' "Forward these WAN ports on your router → ${LAN_IP} (this host):"
  hr
  printf '  %-12s %-10s %-40s %s\n' "WAN PORT" "PROTO" "SERVICE" "REQUIRED"
  hr
  printf '  %-12s %-10s %-40s %s\n' "$PORT_MT1" "TCP" "MTProto DC1 (main client entry)" "yes"
  printf '  %-12s %-10s %-40s %s\n' "$PORT_MT2" "TCP" "MTProto DC2" "yes"
  printf '  %-12s %-10s %-40s %s\n' "$PORT_MT3" "TCP" "MTProto DC3" "yes"
  printf '  %-12s %-10s %-40s %s\n' "$PORT_MT4" "TCP" "MTProto DC4 (media)" "yes"
  printf '  %-12s %-10s %-40s %s\n' "$PORT_STUN" "TCP+UDP" "STUN/TURN (voice/video calls)" "yes"
  printf '  %-12s %-10s %-40s %s\n' "${PORT_RELAY_MIN}-${PORT_RELAY_MAX}" "UDP" "TURN relay media" "yes"
  if [[ "${ENABLE_PASSKEY}" == "yes" ]]; then
    printf '  %-12s %-10s %-40s %s\n' "$PORT_HTTPS" "TCP" "HTTPS (passkey / web)" "yes*"
  else
    printf '  %-12s %-10s %-40s %s\n' "$PORT_HTTPS" "TCP" "HTTPS (passkey / web)" "optional"
  fi
  if [[ "${ENABLE_RTMP}" == "yes" ]]; then
    printf '  %-12s %-10s %-40s %s\n' "$PORT_RTMP" "TCP" "RTMP live streaming" "optional"
    printf '  %-12s %-10s %-40s %s\n' "$PORT_RTMP_HLS" "TCP" "RTMP HLS playback" "optional"
  fi
  hr
  printf '\n%sClient connection IP:%s %s (use this in Android/Desktop builds — not the LAN IP)\n' \
    "${C_BOLD}" "${C_RESET}" "${PUBLIC_IP}"
  if [[ "${ENABLE_PASSKEY}" == "yes" ]]; then
    printf '\n%sPasskey / NPM setup:%s\n' "${C_BOLD}" "${C_RESET}"
    printf '%s\n' \
      "  1. DNS A record: ${PASSKEY_DOMAIN} → ${PUBLIC_IP} (Cloudflare: DNS only, grey cloud)" \
      "  2. Reverse proxy ${PASSKEY_DOMAIN}:443 → ${LAN_IP}:${PORT_HTTPS}" \
      "  3. App__PasskeyRpId is set to ${PASSKEY_DOMAIN} in .env"
  fi
  printf '\n%sDo NOT:%s\n' "${C_YELLOW}" "${C_RESET}"
  printf '%s\n' \
    "  • Put ${LAN_IP} in DcOptions — remote clients cannot reach it" \
    "  • Orange-cloud / proxy MTProto ports in Cloudflare" \
    "  • Route MTProto through Nginx Proxy Manager"
  printf '\n%sGHCR images:%s Make packages public at github.com/CyberoniOntoni/testgram/packages\n' \
    "${C_BOLD}" "${C_RESET}"
  printf '  or run: docker login ghcr.io\n'
}

print_port_forwards

# ── Start stack (optional) ────────────────────────────────────────────────────

start_stack() {
  log "Pulling Docker images (several minutes on first run)..."
  docker compose pull

  log "Starting Testgram stack..."
  docker compose up -d

  log "Waiting for gateway-server (up to 120s)..."
  local i
  for i in $(seq 1 24); do
    if docker compose logs gateway-server 2>/dev/null | grep -q "${PORT_MT1}"; then
      log "Gateway is listening on port ${PORT_MT1}"
      return 0
    fi
    sleep 5
  done
  warn "Gateway did not log port ${PORT_MT1} yet — check: docker compose logs gateway-server"
}

if [[ "${DO_START}" == true ]]; then
  start_stack
  log "Service status:"
  docker compose ps
elif [[ "${NON_INTERACTIVE}" != true ]]; then
  printf '\n'
  if confirm "Start the Docker stack now?"; then
    start_stack
    docker compose ps
  else
    log "Stack not started. When ready:"
    printf '    cd %s && docker compose up -d\n' "${COMPOSE_DIR}"
  fi
fi

# ── Done ──────────────────────────────────────────────────────────────────────

printf '\n%s%sInstallation complete%s\n\n' "${C_GREEN}${C_BOLD}" "" "${C_RESET}"
printf '  Config file:     %s/.env\n' "${COMPOSE_DIR}"
printf '  Install summary: %s\n' "${SUMMARY_FILE}"
printf '  Full guide:      %s/deploy/DEPLOYMENT-example.md\n' "${INSTALL_DIR}"
printf '\n%sNext steps:%s\n' "${C_BOLD}" "${C_RESET}"
printf '%s\n' \
  "  1. Configure router port forwards (table above)" \
  "  2. Open your @BotFather bot → /start → link phone number" \
  "  3. Build clients with server IP: ${PUBLIC_IP}" \
  "  4. Logs: cd ${COMPOSE_DIR} && docker compose logs -f"
printf '\n'