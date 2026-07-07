# Example Testgram Deployment — Complete Setup & Configuration Guide

Self-hosted [Testgram](https://github.com/CyberoniOntoni/testgram) on Proxmox for **AcmeChat**, with internet access, Cloudflare DNS, and optional Nginx Proxy Manager (NPM).

## Deployment summary

| Item | Value |
|------|-------|
| Fork / repo | `https://github.com/CyberoniOntoni/testgram` (branch `dev`) |
| Proxmox VM LAN IP | `192.168.1.10` |
| Public WAN IP | `123.123.123.123` |
| Domain | `acmechat.example` |
| Subdomain (passkey / NPM) | `tg.acmechat.example` |
| NPM host | `192.168.1.11` |
| Brand name | `AcmeChat` |
| Docker images | `ghcr.io/cyberoniontoni/testgram/*` + `mytelegram/*` (session/file) |

---

## 1. Architecture

```text
Internet clients (Android / Desktop)
        │
        │  MTProto TCP :20443, :20543, :20643, :20644
        │  WebRTC UDP/TCP :5348, :49152-49172
        ▼
Router (port forward WAN → 192.168.1.10)
        ▼
Debian VM 192.168.1.10 (Docker Compose)
  ├── gateway-server      (MTProto entry)
  ├── auth/session/messenger/file/...
  ├── mongodb, redis, rabbitmq, minio
  ├── coturn              (voice/video calls)
  ├── bot                 (login codes via Telegram)
  └── minio-proxy         (file download fix)

Optional:
  Cloudflare DNS-only: tg.acmechat.example → 123.123.123.123
  NPM 192.168.1.11:443 → 192.168.1.10:30443 (passkey HTTPS only)
```

### IP vs domain policy

| Use public IP `123.123.123.123` | Use domain `tg.acmechat.example` |
|--------------------------------|---------------------------|
| `App__DcOptions__*_IpAddress` (MTProto) | `App__PasskeyRpId` (WebAuthn) |
| `App__WebRtcConnections__0__Ip` (Coturn) | NPM proxy host + Let's Encrypt |
| Client builds (`YOUR_SERVER_IP`) | — |

**Do not** orange-cloud (proxy) MTProto ports in Cloudflare — it cannot pass raw MTProto TCP.

**Do not** put the LAN IP `192.168.1.10` in `DcOptions` — remote clients cannot reach it.

---

## 2. Prerequisites

### Hardware (Proxmox VM)

| Resource | Minimum |
|----------|---------|
| OS | Debian 12 (bookworm) |
| vCPU | 4 |
| RAM | 8 GB |
| Disk | 80 GB |
| Network | Static IP `192.168.1.10/24` |

### Accounts & services

- GitHub access to pull images from `ghcr.io/cyberoniontoni/testgram`
- Telegram account + [@BotFather](https://t.me/BotFather) bot token
- Cloudflare account with `acmechat.example` (free tier, optional but recommended for passkey)
- Router admin access for port forwarding
- (Optional) Nginx Proxy Manager at `192.168.1.11`

### Router port forwards → `192.168.1.10`

| WAN port | Protocol | Service |
|----------|----------|---------|
| 20443 | TCP | MTProto DC1 (main client entry) |
| 20543 | TCP | MTProto |
| 20643 | TCP | MTProto |
| 20644 | TCP | MTProto |
| 5348 | TCP + UDP | STUN/TURN (non-default; avoids Microsoft 3478–3481 range) |
| 49152–49172 | UDP | TURN relay media |
| 30443 | TCP | HTTPS web endpoint (optional, passkey) |
| 1935 | TCP | RTMP live (optional) |
| 8888 | TCP | RTMP HLS (optional) |

---

## 3. Create the Proxmox VM

1. In Proxmox: **Create VM** → Debian 12 netinst ISO.
2. Set static networking on the guest:

   ```text
   Address:   192.168.1.10
   Netmask:   255.255.255.0
   Gateway:   192.168.1.1        # your LAN gateway
   DNS:       1.1.1.1 / 8.8.8.8
   ```

3. Install only: SSH server, standard system utilities (Docker is installed by the bootstrap script).
4. Ensure the VM can reach the internet (`ping 1.1.1.1`, `ping github.com`).

---

## 4. Cloudflare DNS (optional, free tier)

Used for **passkey (WebAuthn)** and human-readable HTTPS — not for MTProto routing.

1. Log in to Cloudflare → zone **acmechat.example**.
2. Add record:

   | Type | Name | Content | Proxy |
   |------|------|---------|-------|
   | A | `tg` | `123.123.123.123` | **DNS only** (grey cloud) |

3. Do **not** enable orange-cloud proxy on this record.

If your WAN IP changes later, update this A record. Keep `123.123.123.123` in `.env` until you confirm clients work with a hostname (not recommended for MTProto initially).

---

## 5. Nginx Proxy Manager (optional — passkey HTTPS)

Only needed if you use **passkey login**. MTProto does not go through NPM.

On NPM (`192.168.1.11`):

1. **Hosts → Proxy Hosts → Add**

   | Field | Value |
   |-------|-------|
   | Domain names | `tg.acmechat.example` |
   | Scheme | `http` |
   | Forward hostname | `192.168.1.10` |
   | Forward port | `30443` |
   | Websockets | On |
   | Block common exploits | On |

2. **SSL** tab → Request a new SSL certificate (Let's Encrypt) → Force SSL.

3. Ensure port `30443` is forwarded on the router to `192.168.1.10` (or expose via NPM if NPM is your only public entry — for passkey, clients hit `tg.acmechat.example:443` on NPM which proxies to the VM).

> If NPM is the public HTTPS entry, you may forward WAN `443` → `192.168.1.11:443` instead of exposing `30443` directly.

---

## 6. Bootstrap the Debian LXC / VM host

SSH into the container:

```bash
ssh root@192.168.1.10
```

### Interactive install (recommended)

`deploy/install.sh` is the Docker interactive wizard — public IP, LAN IP, ports, branding, bot token, then a **port-forward checklist**.

```bash
curl -fsSL https://raw.githubusercontent.com/CyberoniOntoni/testgram/dev/deploy/install.sh -o /tmp/install.sh
sudo bash /tmp/install.sh
```

Do **not** pipe to bash (`curl ... | bash`) — save the file first.

### Non-interactive (AcmeChat defaults)

```bash
PUBLIC_IP=123.123.123.123 \
LAN_IP=192.168.1.10 \
BRAND=AcmeChat \
PASSKEY_DOMAIN=tg.acmechat.example \
ENABLE_PASSKEY=yes \
BOT_TOKEN='YOUR_BOTFATHER_TOKEN' \
sudo bash /tmp/install.sh --non-interactive --start
```

### Or from a cloned repo

```bash
git clone -b dev https://github.com/CyberoniOntoni/testgram.git /opt/testgram
sudo bash /opt/testgram/deploy/install.sh
```

Proxmox LXC: `deploy/install-lxc.sh` runs the same installer with LXC nesting checks.

The installer:

- Installs Docker and dependencies
- Clones/updates the `dev` branch to `/opt/testgram`
- Prompts for WAN IP, LAN IP, ports, brand, passkey domain, and `BOT_TOKEN`
- Creates `.env` from `.env.example` with auto-generated secrets
- Patches Coturn ports/credentials in `docker-compose.yml` to match your choices
- Configures UFW (MTProto, STUN/TURN on **5348** by default, relay ports)
- Prints required router port forwards → your LAN IP
- With `--start` (or when you confirm at the end): pulls GHCR images and runs `docker compose up -d`

### Proxmox LXC requirements

Docker needs nesting enabled on the CT:

```bash
# On Proxmox host:
pct set <CTID> -features nesting=1,keyctl=1
# Restart the container after changing features
```

Or use a **privileged** LXC container.

---

## 7. GHCR Docker images

Custom-built services are published by GitHub Actions to:

```text
ghcr.io/cyberoniontoni/testgram/mytelegram-messenger-command-server:latest
ghcr.io/cyberoniontoni/testgram/mytelegram-messenger-query-server:latest
ghcr.io/cyberoniontoni/testgram/mytelegram-gateway-server:latest
ghcr.io/cyberoniontoni/testgram/mytelegram-auth-server:latest
ghcr.io/cyberoniontoni/testgram/mytelegram-sms-sender:latest
ghcr.io/cyberoniontoni/testgram/mytelegram-data-seeder:latest
ghcr.io/cyberoniontoni/testgram/testgram-bot:latest
```

Upstream images (not built by the fork):

```text
mytelegram/mytelegram-session-server:latest
mytelegram/mytelegram-file-server:latest
```

### Make packages public (one-time)

GitHub → your profile → **Packages** → each `cyberoniontoni/testgram/*` package → **Package settings** → **Change visibility** → Public.

Or on the VM, log in:

```bash
echo YOUR_GITHUB_PAT | docker login ghcr.io -u CyberoniOntoni --password-stdin
```

### Verify CI built images

Check: https://github.com/CyberoniOntoni/testgram/actions — workflow **Build and Push Docker Images** must be green on `dev`.

---

## 8. Generate secrets

On the VM:

```bash
# Random hex passwords (RabbitMQ, Minio, AccessHashSecretKey)
openssl rand -hex 32

# Base64 encryption keys (MessageKeys, IndexKeys)
openssl rand -base64 32
```

Run once per `CHANGE_ME` field in `.env`.

---

## 9. Configure `.env`

```bash
nano /opt/testgram/docker/compose/.env
```

Template: `docker/compose/.env.deployment.example` (already filled with your IPs and branding).

### Required edits

| Variable | Action |
|----------|--------|
| `RabbitMQ__Connections__Default__Password` | Strong random password |
| `Minio__SecretKey` | Strong random password |
| `App__AccessHashSecretKey` | `openssl rand -hex 32` |
| `App__EncryptionConfig__MessageKeys__0__Key` | `openssl rand -base64 32` |
| `App__EncryptionConfig__IndexKeys__0__Key` | `openssl rand -base64 32` |
| `BOT_TOKEN` | Token from @BotFather |

### Already correct (do not change unless your IP changes)

```bash
App__DcOptions__*_IpAddress=123.123.123.123
App__WebRtcConnections__0__Ip=123.123.123.123
App__PasskeyRpId=tg.acmechat.example
App__Brand=AcmeChat
App__Servers__0__Enabled=True
Minio__BucketName=tg-files
```

### Branding (optional tweaks)

```bash
App__WelcomeMsg=Welcome to AcmeChat! Your account has been created.
App__SendWelcomeMessageAfterUserSignIn=True
App__ProtectedUsernames__0=admin
App__ProtectedUsernames__1=botfather
# add more: App__ProtectedUsernames__4=support
```

### Testing only (not for production)

```bash
App__FixedVerifyCode=12345
```

Skips SMS/bot delivery; remove before real use.

### Bot cannot reach Telegram API

If the VM is in a restricted network, set a proxy in `.env`:

```bash
PROXY_URL=socks5://user:pass@host:1080
```

---

## 10. Verification bot setup

The `bot` service delivers login codes to users who linked their phone via `/start` in Telegram.

1. Open [@BotFather](https://t.me/BotFather) → `/newbot` → copy token.
2. Paste into `.env` as `BOT_TOKEN=...`
3. After stack is up, open your bot in Telegram → send `/start` → follow prompts to link your phone number.
4. On login to AcmeChat, the code arrives in that Telegram chat.

Flow:

```text
auth.sendCode → sms-sender → http://bot:5005/send → Telegram message
```

---

## 11. Start the stack

```bash
cd /opt/testgram/docker/compose
docker compose pull
docker compose up -d
```

First start takes several minutes (Mongo init, data-seeder, image pulls). Watch progress:

```bash
docker compose ps
docker compose logs -f --tail=50
```

### Verify gateway listens on MTProto

```bash
docker compose logs gateway-server | grep 20443
# expect: Tcp server started at ...:20443
```

### Verify from outside your LAN

From a machine **not** on `192.168.1.0/24`:

```bash
nc -zv 123.123.123.123 20443
nc -zv 123.123.123.123 20543
```

If `Connection refused`, check router port forwards and VM firewall (`ufw status`).

---

## 12. Build & configure clients

Fork clients must point at your server IP at **build time**.

| Platform | Repository |
|----------|------------|
| Android | https://github.com/glebxdlolreal/testgram-android |
| Desktop | https://github.com/CyberoniOntoni/familygram-desktop (`dev` — AcmeChat IP pre-patched) |

1. Clone the client repo (`dev` branch).
2. **Desktop:** see [testgram-tdesktop/docs/BUILD-AcmeChat.md](https://github.com/CyberoniOntoni/familygram-desktop/blob/dev/docs/BUILD-AcmeChat.md) — verify `mtproto_dc_options.cpp` has `123.123.123.123` and ports `20443`/`20543`/`20643`.
3. **Android:** search for `YOUR_SERVER_IP` and replace with **`123.123.123.123`** (public IP, not domain).
4. Build and install the APK / desktop binary (Windows: Visual Studio + `prepare\win.bat` + `configure.bat x64` with your `api_id`/`api_hash` from [my.telegram.org](https://my.telegram.org/apps)).

Official MyTelegram iOS/Web clients from `loyldg` may need separate RSA key / DC configuration — Android and TDesktop forks above are the documented path.

---

## 13. First login checklist

1. Install your custom-built client.
2. Enter your real phone number (international format, e.g. `+1...`).
3. Ensure you `/start`’d the verification bot and linked that number.
4. Receive the 5-digit code in Telegram (or use `App__FixedVerifyCode` during testing).
5. Complete signup — if `App__SendWelcomeMessageAfterUserSignIn=True`, you get the welcome DM from the official notification account.

---

## 14. Troubleshooting

### `ConnectionRefusedError` on connect

**Cause:** Gateway not listening on `20443`.

**Fix:**

```bash
# .env must have (not commented, not empty):
App__Servers__0__Enabled=True

docker compose up -d --force-recreate gateway-server
docker compose logs gateway-server | grep 20443
```

### `could not choose public RSA key` / wrong DC

**Cause:** Missing DC entries or dead ports.

**Fix:** Ensure all four DcOptions IPs are `123.123.123.123` and ports `20443/20543/20643/20644` are forwarded and listening.

### `Bucket name cannot be empty` / media won't load

**Fix:**

```bash
Minio__BucketName=tg-files
Minio__CreateBucketIfNotExists=True
docker compose up -d --force-recreate file-server
```

### File download stalls / `NullReferenceException` in file-server

**Fix:** Ensure `minio-proxy` is running:

```bash
docker compose up -d minio-proxy
docker compose up -d --force-recreate file-server
```

### Login code never arrives

1. Check `bot` container: `docker compose logs bot`
2. Check `sms-sender`: `docker compose logs sms-sender`
3. Confirm `BOT_TOKEN` is valid.
4. Confirm you linked the phone via `/start` in the bot.
5. Set `PROXY_URL` if the VM cannot reach `api.telegram.org`.

### Voice/video calls fail

1. Confirm Coturn ports forwarded (`5348` TCP+UDP, `49152-49172/udp`).
2. Confirm `.env` WebRTC IP is `123.123.123.123`.
3. Credentials must match docker-compose coturn: `testgram` / `testgram2024`.

### Passkey login fails

1. `App__PasskeyRpId` must exactly match the HTTPS domain (`tg.acmechat.example`).
2. NPM must serve valid Let's Encrypt cert on that domain.
3. Port `30443` reachable from NPM → VM.

---

## 15. Maintenance

### Update images

```bash
cd /opt/testgram
git pull
cd docker/compose
docker compose pull
docker compose up -d
```

### View logs

```bash
docker compose logs -f gateway-server
docker compose logs -f messenger-command-server
docker compose logs -f bot
```

### Backup data

Important paths under `/opt/testgram/docker/compose/data/`:

```text
data/mongo/          # user accounts, messages
data/minio/          # uploaded media
data/bot/            # verification bot phone bindings
data/mytelegram/     # service logs
```

Example backup:

```bash
tar -czf /root/testgram-backup-$(date +%F).tar.gz \
  /opt/testgram/docker/compose/data \
  /opt/testgram/docker/compose/.env
```

### Admin: give stars to a user

```bash
docker compose exec mongodb mongosh tg
```

```javascript
db['star-transactions'].insertOne({
  UserId: Long('USER_ID'),
  Amount: 1000,
  Gift: false,
  Title: 'Admin top-up',
  PeerUserId: 0,
  Date: new Date()
});
db['eventflow-userreadmodel'].updateOne(
  { UserId: Long('USER_ID') },
  { $inc: { StarsBalance: 1000 } }
);
```

Find `USER_ID`:

```javascript
db['eventflow-userreadmodel'].find({ UserName: 'someusername' })
```

---

## 16. Quick reference commands

```bash
# Status
docker compose ps

# Restart everything
docker compose restart

# Restart one service
docker compose restart gateway-server

# Follow all logs
docker compose logs -f

# Re-create after .env change
docker compose up -d --force-recreate messenger-command-server messenger-query-server gateway-server

# Stop
docker compose down

# Stop and remove volumes (DESTRUCTIVE)
docker compose down -v
```

---

## 17. File map

| Path | Purpose |
|------|---------|
| `docker/compose/docker-compose.yml` | Full stack definition |
| `docker/compose/.env.deployment.example` | Your pre-filled deployment template |
| `docker/compose/.env` | Live secrets (never commit) |
| `deploy/setup-debian.sh` | VM bootstrap script |
| `deploy/DEPLOYMENT-AcmeChat.md` | This guide |
| `.github/workflows/docker-build.yml` | CI → GHCR image builds |

---

## Support & upstream

- Fork: https://github.com/CyberoniOntoni/testgram
- Upstream: https://github.com/glebxdlolreal/testgram
- Calls setup: `docs/CALLS_SETUP.md`
- General README: `README.md`
