# Shared helpers for Testgram Docker installers.
# Sourced by deploy/install.sh — do not execute directly.

installer_lib_init() {
  if [[ -t 1 ]] || [[ -w /dev/tty ]]; then
    C_RESET=$'\033[0m'
    C_BOLD=$'\033[1m'
    C_DIM=$'\033[2m'
    C_GREEN=$'\033[32m'
    C_YELLOW=$'\033[33m'
    C_CYAN=$'\033[36m'
    C_RED=$'\033[31m'
  else
    C_RESET='' C_BOLD='' C_DIM='' C_GREEN='' C_YELLOW='' C_CYAN='' C_RED=''
  fi
}

interactive_tty() {
  [[ "${NON_INTERACTIVE}" != true ]] && [[ -r /dev/tty ]] && [[ -w /dev/tty ]]
}

ui_printf() {
  if interactive_tty; then
    # shellcheck disable=SC2059
    printf "$@" > /dev/tty
  else
    # shellcheck disable=SC2059
    printf "$@"
  fi
}

ui_warn() {
  if interactive_tty; then
    # shellcheck disable=SC2059
    printf "$@" > /dev/tty
  else
    # shellcheck disable=SC2059
    printf "$@" >&2
  fi
}

hr()  { ui_printf '%s\n' "────────────────────────────────────────────────────────────"; }
log() { ui_printf '%s==>%s %s\n' "${C_GREEN}" "${C_RESET}" "$*"; }
warn() { ui_warn '%s!!%s %s\n' "${C_YELLOW}" "${C_RESET}" "$*"; }
die()  { ui_warn '%sERROR:%s %s\n' "${C_RED}" "${C_RESET}" "$*"; exit 1; }

banner() {
  ui_printf '\n%s' "${C_CYAN}${C_BOLD}"
  if interactive_tty; then
    cat <<'EOF' > /dev/tty
████████╗███████╗███████╗████████╗ ██████╗ ██████╗  █████╗ ███╗   ███╗
╚══██╔══╝██╔════╝██╔════╝╚══██╔══╝██╔════╝ ██╔══██╗██╔══██╗████╗ ████║
   ██║   █████╗  ███████╗   ██║   ██║  ███╗██████╔╝███████║██╔████╔██║
   ██║   ██╔══╝  ╚════██║   ██║   ██║   ██║██╔══██╗██╔══██║██║╚██╔╝██║
   ██║   ███████╗███████║   ██║   ╚██████╔╝██║  ██║██║  ██║██║ ╚═╝ ██║
   ╚═╝   ╚══════╝╚══════╝   ╚═╝    ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝     ╚═╝
EOF
  else
    cat <<'EOF'
████████╗███████╗███████╗████████╗ ██████╗ ██████╗  █████╗ ███╗   ███╗
╚══██╔══╝██╔════╝██╔════╝╚══██╔══╝██╔════╝ ██╔══██╗██╔══██╗████╗ ████║
   ██║   █████╗  ███████╗   ██║   ██║  ███╗██████╔╝███████║██╔████╔██║
   ██║   ██╔══╝  ╚════██║   ██║   ██║   ██║██╔══██╗██╔══██║██║╚██╔╝██║
   ██║   ███████╗███████║   ██║   ╚██████╔╝██║  ██║██║  ██║██║ ╚═╝ ██║
   ╚═╝   ╚══════╝╚══════╝   ╚═╝    ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝     ╚═╝
EOF
  fi
  ui_printf '%s\n' "${C_RESET}"
  ui_printf '  %sDocker interactive installer%s\n' "${C_BOLD}" "${C_RESET}"
  ui_printf '  %sSelf-hosted Telegram-compatible server via Docker Compose%s\n\n' "${C_DIM}" "${C_RESET}"
}

step() {
  ui_printf '\n%s Step %d/%d — %s%s\n' "${C_CYAN}${C_BOLD}" "$1" "$2" "$3" "${C_RESET}"
  hr
}

escape_sed_repl() {
  local s="$1"
  s="${s//\\/\\\\}"
  s="${s//&/\\&}"
  s="${s//|/\\|}"
  printf '%s' "$s"
}

sanitize_ip_input() {
  local s="$1"
  s="$(trim_line "$s")"
  s="${s//[^0-9.]/}"
  printf '%s' "$s"
}

is_ipv4() {
  local ip="$1"
  local a b c d extra o
  local re='^([0-9]{1,3}\.){3}[0-9]{1,3}$'

  ip="$(sanitize_ip_input "$ip")"
  [[ -n "$ip" ]] || return 1
  [[ "$ip" =~ $re ]] || return 1

  IFS='.' read -r a b c d extra <<< "${ip}."
  [[ -n "$a" && -n "$b" && -n "$c" && -n "$d" && -z "$extra" ]] || return 1

  for o in "$a" "$b" "$c" "$d"; do
    [[ "$o" =~ ^[0-9]{1,3}$ ]] || return 1
    if (( o > 255 )); then
      return 1
    fi
  done
  return 0
}

is_domain() {
  local d re
  d="$(trim_line "$1")"
  re='^([a-zA-Z0-9]([a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$'
  [[ "$d" =~ $re ]] || return 1
  return 0
}

is_port() {
  local p="$1"
  local re='^[0-9]+$'
  [[ "$p" =~ $re ]] || return 1
  if (( p < 1 || p > 65535 )); then
    return 1
  fi
  return 0
}

is_yes() {
  case "${1,,}" in
    true|yes|1) return 0 ;;
    *) return 1 ;;
  esac
}

detect_lan_ip() {
  ip -4 route get 1.1.1.1 2>/dev/null | awk '{for (i=1;i<=NF;i++) if ($i=="src") {print $(i+1); exit}}' || true
}

setup_interactive_stdin() {
  if [[ "${NON_INTERACTIVE}" == true ]]; then
    return 0
  fi
  if [[ -r /dev/tty ]] && [[ -w /dev/tty ]]; then
    exec 0</dev/tty
    return 0
  fi
  if [[ -t 0 ]]; then
    return 0
  fi
  die "No interactive terminal.

Do NOT pipe this script (curl ... | bash) — that steals stdin and skips prompts.

Instead:
  curl -fsSL https://raw.githubusercontent.com/CyberoniOntoni/testgram/dev/deploy/install.sh -o install.sh
  bash install.sh

Or pass all values explicitly:
  PUBLIC_IP=... LAN_IP=... BOT_TOKEN=... bash install.sh --non-interactive"
}

trim_line() {
  local s="$1"
  s="${s//$'\r'/}"
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  printf '%s' "$s"
}

read_with_prompt() {
  local __var="$1"
  local __prompt="$2"
  local __line=""
  # -p keeps prompt and input on one line; -e needs readline (fallback without -e)
  if interactive_tty; then
    if ! IFS= read -r -e -p "$__prompt" __line < /dev/tty 2>/dev/null; then
      if ! IFS= read -r -p "$__prompt" __line < /dev/tty; then
        die "Could not read input from /dev/tty. Try: ssh -t root@host"
      fi
    fi
  elif ! IFS= read -r -p "$__prompt" __line; then
    die "Could not read input. Use an SSH session with a TTY (ssh -t root@host)."
  fi
  __line="$(trim_line "$__line")"
  printf -v "$__var" '%s' "$__line"
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
    read_with_prompt input "  ${prompt_text}${display_default}: "
    input="${input:-$default}"
    if [[ -z "$input" ]]; then
      warn "This field is required."
      continue
    fi
    if [[ -n "$validate" ]]; then
      if ! "$validate" "$input"; then
        if [[ "$validate" == "is_ipv4" ]]; then
          warn "Invalid IPv4 — use four decimal octets, e.g. 203.0.113.50"
        elif [[ "$validate" == "is_domain" ]]; then
          warn "Invalid domain — use e.g. tg.example.com (no https://)"
        else
          warn "Invalid value — try again."
        fi
        continue
      fi
      if [[ "$validate" == "is_ipv4" ]]; then
        input="$(sanitize_ip_input "$input")"
      fi
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
    read_with_prompt input "  ${prompt_text} ${C_DIM}(${hint})${C_RESET}: "
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
  read_with_prompt input "  ${prompt_text} (Y/n): "
  input="${input:-$default}"
  [[ "${input,,}" == "y" || "${input,,}" == "yes" ]]
}

check_lxc_prereqs() {
  if [[ "${INSTALLER_PROFILE:-}" != "lxc" ]]; then
    return 0
  fi
  if [[ -f /.dockerenv ]] || grep -qE 'lxc|container' /proc/1/cgroup 2>/dev/null; then
    log "LXC/container environment detected"
    if ! grep -q '^Features:.*nesting' /proc/self/status 2>/dev/null; then
      warn "LXC nesting may be disabled — Docker can fail."
      warn "On Proxmox host: pct set <CTID> -features nesting=1,keyctl=1"
      warn "Then restart the container."
    fi
  fi
}

maybe_self_update() {
  local repo_script="${LOCAL_REPO_ROOT}/deploy/install.sh"
  [[ -f "${repo_script}" ]] || return 0
  [[ -d "${LOCAL_REPO_ROOT}/.git" ]] || return 0
  [[ "${INSTALLER_SELF_UPDATED:-}" == "1" ]] && return 0

  local hash_before hash_after
  hash_before="$(sha256sum "${repo_script}" 2>/dev/null | awk '{print $1}')" || return 0

  git -C "${LOCAL_REPO_ROOT}" fetch origin "${REPO_BRANCH}" 2>/dev/null || true
  git -C "${LOCAL_REPO_ROOT}" checkout "${REPO_BRANCH}" 2>/dev/null || true
  git -C "${LOCAL_REPO_ROOT}" pull --ff-only origin "${REPO_BRANCH}" 2>/dev/null || true

  hash_after="$(sha256sum "${repo_script}" 2>/dev/null | awk '{print $1}')" || return 0
  if [[ "${hash_before}" != "${hash_after}" ]]; then
    log "Installer updated from git — restarting..."
    export INSTALLER_SELF_UPDATED=1
    exec bash "${repo_script}" "$@"
  fi
}

install_system_deps() {
  if command -v apt-get >/dev/null 2>&1; then
    export DEBIAN_FRONTEND=noninteractive
    log "Installing system packages (apt)..."
    apt-get update -qq
    apt-get install -y -qq ca-certificates curl git gnupg openssl iproute2
    if is_yes "${DO_FIREWALL}"; then
      apt-get install -y -qq ufw || warn "UFW not available — skipping firewall packages"
    fi
    return 0
  fi
  warn "apt-get not found — ensure curl, git, and openssl are installed"
}

install_docker() {
  if command -v docker >/dev/null 2>&1; then
    log "Docker already installed: $(docker --version)"
    return 0
  fi
  if ! is_yes "${INSTALL_DOCKER}"; then
    die "Docker is not installed. Re-run and choose to install Docker, or install it manually."
  fi
  log "Installing Docker (official script)..."
  curl -fsSL https://get.docker.com | sh
}

ensure_compose() {
  docker compose version >/dev/null 2>&1 || die "docker compose plugin missing — install docker-compose-plugin"
}

clone_or_update_repo() {
  log "Cloning or updating Testgram (${REPO_BRANCH}) → ${INSTALL_DIR}"
  if [[ -d "${INSTALL_DIR}/.git" ]]; then
    git -C "${INSTALL_DIR}" fetch origin
    git -C "${INSTALL_DIR}" checkout "${REPO_BRANCH}"
    git -C "${INSTALL_DIR}" pull --ff-only origin "${REPO_BRANCH}" || warn "git pull failed — using existing checkout"
  else
    git clone --branch "${REPO_BRANCH}" --depth 1 "${REPO_URL}" "${INSTALL_DIR}"
  fi
}

set_env() {
  local key="$1" val="$2" escaped
  escaped="$(escape_sed_repl "$val")"
  if grep -q "^${key}=" .env; then
    sed -i "s|^${key}=.*|${key}=${escaped}|" .env
  else
    printf '%s=%s\n' "${key}" "${val}" >> .env
  fi
}

write_env_file() {
  log "Writing ${COMPOSE_DIR}/.env ..."
  if [[ -f .env ]]; then
    cp .env ".env.bak.$(date +%s)"
    warn "Existing .env backed up before overwrite"
  fi
  cp .env.example .env

  local rabbit_pw minio_sk access_hash msg_key idx_key
  rabbit_pw="$(openssl rand -hex 24)"
  minio_sk="$(openssl rand -hex 24)"
  access_hash="$(openssl rand -hex 32)"
  msg_key="$(openssl rand -base64 32)"
  idx_key="$(openssl rand -base64 32)"

  set_env RabbitMQ__Connections__Default__Password "$rabbit_pw"
  set_env Minio__SecretKey "$minio_sk"
  set_env App__AccessHashSecretKey "$access_hash"
  set_env App__EncryptionConfig__MessageKeys__0__Key "$msg_key"
  set_env App__EncryptionConfig__IndexKeys__0__Key "$idx_key"

  set_env App__Brand "$BRAND"
  set_env App__WelcomeMsg "Welcome to ${BRAND}! Your account has been created."
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

  local i
  for i in 0 1 2 3; do
    set_env "App__DcOptions__${i}__IpAddress" "$PUBLIC_IP"
  done
  set_env App__DcOptions__0__Port "$PORT_MT1"
  set_env App__DcOptions__1__Port "$PORT_MT2"
  set_env App__DcOptions__2__Port "$PORT_MT3"
  set_env App__DcOptions__3__Port "$PORT_MT4"

  set_env RTMP_PORT "$PORT_RTMP"
  set_env RTMP_HLS_PORT "$PORT_RTMP_HLS"
}

patch_compose() {
  local file="$1" turn_pass_escaped
  [[ -f "$file" ]] || die "Missing ${file}"
  turn_pass_escaped="$(escape_sed_repl "${TURN_PASS}")"

  sed -i \
    -e "s|\"5348:5348\"|\"${PORT_STUN}:${PORT_STUN}\"|g" \
    -e "s|\"5348:5348/udp\"|\"${PORT_STUN}:${PORT_STUN}/udp\"|g" \
    -e "s|49152-49172:49152-49172|${PORT_RELAY_MIN}-${PORT_RELAY_MAX}:${PORT_RELAY_MIN}-${PORT_RELAY_MAX}|g" \
    -e "s|--listening-port 5348|--listening-port ${PORT_STUN}|g" \
    -e "s|--min-port 49152|--min-port ${PORT_RELAY_MIN}|g" \
    -e "s|--max-port 49172|--max-port ${PORT_RELAY_MAX}|g" \
    -e "s|--user testgram:testgram2024|--user ${TURN_USER}:${turn_pass_escaped}|g" \
    "$file"
}

prepare_data_dirs() {
  log "Preparing data directories..."
  mkdir -p data/mytelegram data/bot geoip data/redis data/rabbitmq \
    data/mongo/db data/mongo/configdb data/minio data/coturn data/rtmp
  chmod -R a+w data
}

save_install_summary() {
  SUMMARY_FILE="${INSTALL_DIR}/deploy/last-install-config.txt"
  mkdir -p "${INSTALL_DIR}/deploy"
  cat > "${SUMMARY_FILE}" <<EOF
# Testgram install config — $(date -Iseconds)
INSTALLER_VERSION=${INSTALLER_VERSION}
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
}

configure_firewall() {
  if ! is_yes "${DO_FIREWALL}"; then
    log "Skipping firewall configuration"
    return 0
  fi
  if ! command -v ufw >/dev/null 2>&1; then
    warn "UFW not installed — configure your firewall manually"
    return 0
  fi
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
}

print_port_forwards() {
  ui_printf '\n%sRouter / firewall — open these ports%s\n' "${C_CYAN}${C_BOLD}" "${C_RESET}"
  ui_printf '%s\n' "Forward WAN → ${LAN_IP} on your router (or allow on cloud firewall):"
  hr
  ui_printf '  %-14s %-10s %-38s %s\n' "PORT" "PROTO" "SERVICE" "REQUIRED"
  hr
  ui_printf '  %-14s %-10s %-38s %s\n' "$PORT_MT1" "TCP" "MTProto DC1 (main client entry)" "yes"
  ui_printf '  %-14s %-10s %-38s %s\n' "$PORT_MT2" "TCP" "MTProto DC2" "yes"
  ui_printf '  %-14s %-10s %-38s %s\n' "$PORT_MT3" "TCP" "MTProto DC3" "yes"
  ui_printf '  %-14s %-10s %-38s %s\n' "$PORT_MT4" "TCP" "MTProto DC4 (media)" "yes"
  ui_printf '  %-14s %-10s %-38s %s\n' "$PORT_STUN" "TCP+UDP" "STUN/TURN (voice/video)" "yes"
  ui_printf '  %-14s %-10s %-38s %s\n' "${PORT_RELAY_MIN}-${PORT_RELAY_MAX}" "UDP" "TURN relay media" "yes"
  if [[ "${ENABLE_PASSKEY}" == "yes" ]]; then
    ui_printf '  %-14s %-10s %-38s %s\n' "$PORT_HTTPS" "TCP" "HTTPS (passkey / web)" "yes"
  else
    ui_printf '  %-14s %-10s %-38s %s\n' "$PORT_HTTPS" "TCP" "HTTPS (passkey / web)" "optional"
  fi
  if [[ "${ENABLE_RTMP}" == "yes" ]]; then
    ui_printf '  %-14s %-10s %-38s %s\n' "$PORT_RTMP" "TCP" "RTMP live streaming" "optional"
    ui_printf '  %-14s %-10s %-38s %s\n' "$PORT_RTMP_HLS" "TCP" "RTMP HLS playback" "optional"
  fi
  hr
  ui_printf '\n%sClient build IP:%s %s\n' "${C_BOLD}" "${C_RESET}" "${PUBLIC_IP}"
  ui_printf '%sNever put the LAN IP (%s) in DcOptions — remote clients cannot reach it.%s\n' \
    "${C_YELLOW}" "${LAN_IP}" "${C_RESET}"
  if [[ "${ENABLE_PASSKEY}" == "yes" ]]; then
    ui_printf '\n%sPasskey setup:%s\n' "${C_BOLD}" "${C_RESET}"
    ui_printf '%s\n' \
      "  • DNS A: ${PASSKEY_DOMAIN} → ${PUBLIC_IP} (grey cloud / DNS only)" \
      "  • Proxy ${PASSKEY_DOMAIN}:443 → ${LAN_IP}:${PORT_HTTPS}"
  fi
  ui_printf '\n%sGHCR:%s github.com/CyberoniOntoni/testgram/packages must be public, or run docker login ghcr.io\n' \
    "${C_BOLD}" "${C_RESET}"
}

print_config_review() {
  hr
  ui_printf '  %-22s %s\n' "Install directory:" "${INSTALL_DIR}"
  ui_printf '  %-22s %s\n' "Compose directory:" "${COMPOSE_DIR}"
  ui_printf '  %-22s %s\n' "Git branch:" "${REPO_BRANCH}"
  ui_printf '  %-22s %s\n' "Public WAN IP:" "${PUBLIC_IP}"
  ui_printf '  %-22s %s\n' "LAN / host IP:" "${LAN_IP}"
  ui_printf '  %-22s %s\n' "Brand:" "${BRAND}"
  ui_printf '  %-22s %s\n' "Passkey:" "${ENABLE_PASSKEY} (${PASSKEY_DOMAIN})"
  ui_printf '  %-22s %s\n' "MTProto:" "${PORT_MT1}, ${PORT_MT2}, ${PORT_MT3}, ${PORT_MT4}"
  ui_printf '  %-22s %s\n' "HTTPS:" "${PORT_HTTPS}"
  ui_printf '  %-22s %s\n' "STUN/TURN:" "${PORT_STUN} tcp+udp, relay ${PORT_RELAY_MIN}-${PORT_RELAY_MAX} udp"
  if [[ "${ENABLE_RTMP}" == "yes" ]]; then
    ui_printf '  %-22s %s\n' "RTMP:" "${PORT_RTMP} / HLS ${PORT_RTMP_HLS}"
  fi
  ui_printf '  %-22s %s\n' "Install Docker:" "${INSTALL_DOCKER}"
  ui_printf '  %-22s %s\n' "Configure UFW:" "${DO_FIREWALL}"
  ui_printf '  %-22s %s\n' "Bot token:" "${BOT_TOKEN:0:12}..."
  hr
}

start_stack() {
  log "Pulling Docker images (first run may take several minutes)..."
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

prompt_bot_token() {
  if [[ -n "${BOT_TOKEN}" ]]; then
    log "BOT_TOKEN provided via environment/flag"
    return 0
  fi
  while true; do
    read_with_prompt BOT_TOKEN "  Bot token from @BotFather: "
    if [[ "$BOT_TOKEN" =~ ^[0-9]+:[A-Za-z0-9_-]+$ ]]; then
      break
    fi
    warn "Token should look like 123456789:AAH..."
  done
}

run_install_wizard() {
  local total_steps=7

  banner
  ui_printf '  %sInstaller v%s%s\n\n' "${C_DIM}" "${INSTALLER_VERSION}" "${C_RESET}"

  step 1 "$total_steps" "Before you start"
  ui_printf '%s\n' \
    "You will need:" \
    "  • Public WAN IP (what clients connect to)" \
    "  • LAN IP of this machine (for router port forwards)" \
    "  • @BotFather bot token (login verification codes)" \
    ""
  ui_printf '%sRules:%s MTProto must go direct to your IP — not through Cloudflare proxy or NPM.\n\n' \
    "${C_YELLOW}" "${C_RESET}"

  check_lxc_prereqs

  if [[ -r /etc/os-release ]]; then
    # shellcheck source=/dev/null
    . /etc/os-release
    log "OS: ${PRETTY_NAME:-unknown}"
  fi

  if ! confirm "Start the interactive installer?"; then
    ui_printf '\nAborted.\n'
    exit 0
  fi

  step 2 "$total_steps" "Where to install"
  prompt INSTALL_DIR "Install directory" "${INSTALL_DIR}"
  COMPOSE_DIR="${INSTALL_DIR}/docker/compose"
  COMPOSE_FILE="${COMPOSE_DIR}/docker-compose.yml"

  step 3 "$total_steps" "Network & branding"
  ui_printf '%s\n' "Public IP goes into client configs. LAN IP is only for port-forward targets." ""
  local detected_lan
  detected_lan="$(detect_lan_ip)"
  prompt PUBLIC_IP "Public WAN IP" "" is_ipv4
  prompt LAN_IP "LAN IP of this host" "${detected_lan:-}" is_ipv4
  prompt BRAND "Brand / app name" "Testgram"
  prompt_yes_no ENABLE_PASSKEY "Enable passkey (WebAuthn)? Needs HTTPS + domain" "no"
  if [[ "${ENABLE_PASSKEY}" == "yes" ]]; then
    prompt PASSKEY_DOMAIN "Passkey domain (e.g. tg.example.com)" "" is_domain
  else
    PASSKEY_DOMAIN="${PASSKEY_DOMAIN:-localhost}"
  fi

  step 4 "$total_steps" "Ports"
  prompt_yes_no CUSTOMIZE_PORTS "Customize service ports? (No = use Testgram defaults)" "no"
  if [[ "${CUSTOMIZE_PORTS}" == "yes" ]]; then
    prompt PORT_MT1 "MTProto DC1 (main)" "$PORT_MT1" is_port
    prompt PORT_MT2 "MTProto DC2" "$PORT_MT2" is_port
    prompt PORT_MT3 "MTProto DC3" "$PORT_MT3" is_port
    prompt PORT_MT4 "MTProto DC4 (media)" "$PORT_MT4" is_port
    prompt PORT_HTTPS "HTTPS / web" "$PORT_HTTPS" is_port
    prompt PORT_STUN "STUN/TURN (tcp+udp)" "$PORT_STUN" is_port
    prompt PORT_RELAY_MIN "TURN relay range start" "$PORT_RELAY_MIN" is_port
    prompt PORT_RELAY_MAX "TURN relay range end" "$PORT_RELAY_MAX" is_port
    if (( PORT_RELAY_MIN >= PORT_RELAY_MAX )); then
      die "Relay range invalid: ${PORT_RELAY_MIN} must be < ${PORT_RELAY_MAX}"
    fi
  else
    log "Using default ports (MTProto 20443-20644, STUN/TURN 5348, relay 49152-49172)"
  fi

  prompt_yes_no ENABLE_RTMP "Expose RTMP live streaming ports?" "no"
  if [[ "${ENABLE_RTMP}" == "yes" ]]; then
    prompt PORT_RTMP "RTMP port" "$PORT_RTMP" is_port
    prompt PORT_RTMP_HLS "RTMP HLS port" "$PORT_RTMP_HLS" is_port
  fi

  step 5 "$total_steps" "Bot & Docker options"
  prompt_bot_token
  TURN_PASS="${TURN_PASS:-$(openssl rand -hex 16)}"
  log "Generated TURN password"

  if command -v docker >/dev/null 2>&1; then
    INSTALL_DOCKER="no"
    log "Docker already installed"
  else
    prompt_yes_no INSTALL_DOCKER "Install Docker via get.docker.com?" "yes"
  fi
  if is_yes "${DO_FIREWALL}"; then
    prompt_yes_no DO_FIREWALL "Configure UFW firewall on this host?" "yes"
  fi

  step 6 "$total_steps" "Review"
  print_config_review
  if ! confirm "Apply configuration and install?"; then
    ui_printf '\nAborted. No changes made.\n'
    exit 0
  fi

  step 7 "$total_steps" "Installing"
}

run_install_apply() {
  install_system_deps
  install_docker
  ensure_compose
  clone_or_update_repo

  cd "${COMPOSE_DIR}"
  write_env_file
  patch_compose "${COMPOSE_FILE}"
  prepare_data_dirs
  save_install_summary
  configure_firewall
  print_port_forwards

  if [[ "${DO_START}" == true ]]; then
    start_stack
    docker compose ps
  elif [[ "${NON_INTERACTIVE}" != true ]]; then
    ui_printf '\n'
    if confirm "Start docker compose now?"; then
      start_stack
      docker compose ps
    else
      log "Stack not started. When ready:"
      ui_printf '    cd %s && docker compose up -d\n' "${COMPOSE_DIR}"
    fi
  fi

  ui_printf '\n%sDone — Testgram Docker stack is ready%s\n\n' "${C_GREEN}${C_BOLD}" "${C_RESET}"
  ui_printf '  .env:      %s/.env\n' "${COMPOSE_DIR}"
  ui_printf '  Summary:   %s\n' "${SUMMARY_FILE}"
  ui_printf '  Logs:      cd %s && docker compose logs -f\n' "${COMPOSE_DIR}"
  ui_printf '  Next:      port-forward table above, link phone in @BotFather bot, build clients with IP %s\n\n' "${PUBLIC_IP}"
}