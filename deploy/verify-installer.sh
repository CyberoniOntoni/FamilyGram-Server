#!/usr/bin/env bash
# Quick sanity checks for the installer (run on Linux or Git Bash).
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

fail() { printf 'FAIL: %s\n' "$*" >&2; FAILED=1; }

FAILED=0

printf '==> bash -n syntax check\n'
bash -n "${SCRIPT_DIR}/install.sh"
bash -n "${SCRIPT_DIR}/lib/installer-lib.sh"
bash -n "${SCRIPT_DIR}/install-lxc.sh"
bash -n "${SCRIPT_DIR}/setup-debian.sh"

printf '==> validator unit tests\n'
export NON_INTERACTIVE=true
# shellcheck source=lib/installer-lib.sh
source "${SCRIPT_DIR}/lib/installer-lib.sh"
installer_lib_init

is_ipv4 "203.0.113.50" || fail "valid public IP rejected"
is_ipv4 "192.168.1.79" || fail "valid LAN IP rejected"
# Regression: 10#o without braces must not error (bash parses as invalid base)
is_ipv4 "203.0.113.50" >/dev/null 2>&1 || fail "re-validation failed"
is_ipv4 "10.0.0.08" || fail "10.0.0.08 should be valid (decimal 08)"
is_ipv4 "10.0.0.1" || fail "10.0.0.1 rejected"
is_ipv4 "8.8.8.8" || fail "8.8.8.8 rejected"
is_ipv4 "0.0.0.0" || fail "0.0.0.0 rejected"
is_ipv4 "172.16.0.1" || fail "172.16.0.1 rejected"
is_ipv4 "256.1.1.1" && fail "256.1.1.1 should fail"
is_ipv4 "1.2.3" && fail "1.2.3 should fail"
is_ipv4 "" && fail "empty should fail"
is_ipv4 "1.2.3.4.5" && fail "five octets should fail"

is_port "20443" || fail "20443 port rejected"
is_port "0" && fail "port 0 should fail"
is_port "65536" && fail "port 65536 should fail"

is_domain "tg.example.com" || fail "domain rejected"
is_domain "not a domain" && fail "invalid domain accepted"

is_yes "yes" || fail "is_yes yes"
is_yes "true" || fail "is_yes true"
is_yes "no" && fail "is_yes no should fail"

escaped="$(escape_sed_repl 'a&b|c\d/e')"
[[ "$escaped" == 'a\&b\|c\\d/e' ]] || fail "escape_sed_repl got: $escaped"

[[ "$(port_with_proto 5348 'TCP&UDP')" == '5348(TCP&UDP)' ]] || fail "port_with_proto TCP&UDP"
[[ "$(port_with_proto 20443 TCP)" == '20443(TCP)' ]] || fail "port_with_proto TCP"
[[ "$(port_with_proto 49152-49172 UDP)" == '49152-49172(UDP)' ]] || fail "port_with_proto UDP range"

printf '==> set_env sed safety\n'
tmpdir="$(mktemp -d)"
trap 'rm -rf "${tmpdir}"' EXIT
printf 'KEY=old\n' > "${tmpdir}/.env"
(
  cd "${tmpdir}"
  set_env KEY 'value&with|special\chars'
)
grep -q '^KEY=value&with|special\\chars$' "${tmpdir}/.env" || fail "set_env mangled special chars"

if [[ "${FAILED}" -eq 0 ]]; then
  printf 'All installer checks passed.\n'
else
  printf 'Some checks failed.\n' >&2
  exit 1
fi