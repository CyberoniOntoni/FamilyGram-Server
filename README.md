# FamilyGram-Server

[![API Layer](https://img.shields.io/badge/API_Layer-224-blueviolet)](https://corefork.telegram.org/methods)
[![MTProto](https://img.shields.io/badge/MTProto_Protocol-2.0-green)](https://corefork.telegram.org/mtproto/)
[![Fork](https://img.shields.io/badge/fork-loyldg%2Fmytelegram-blue)](https://github.com/loyldg/mytelegram)

**FamilyGram-Server** is the MTProto backend for [FamilyGram](https://github.com/CyberoniOntoni/familygram) — a fork of [MyTelegram](https://github.com/loyldg/mytelegram), a self-hosted C# implementation of the Telegram server-side API. (This repo was formerly named **Testgram**.)

## Supported Features

### Open Source Features
- API Layer: `224`
- MTProto Transports: `Abridged`, `Intermediate`
- Private Chat
- Supergroup Chat
- Channel
- Message Reactions
- Star Gifts (channels, hide/show, unread mentions)
- Passkey Login (WebAuthn)
- Channel Direct Messages (Monoforum)
- Bot Support
- Stories
- Privacy Settings & 2FA
- Voice & Video Calls (WebRTC)
- Telegram Business
- Auto-Delete Messages
- Stickers
- Scheduled Messages
- Forum Topics
- Themes & Wallpapers
- Folders (Dialog Filters)

### Soon...
- End-to-End Encrypted Chat
- Email Login
- Email Sender
- Push Notifications (Firebase)

---

## Running FamilyGram Server

### Full deployment guide (Proxmox example)

See **[deploy/DEPLOYMENT-example.md](deploy/DEPLOYMENT-example.md)** for complete Proxmox, Cloudflare, NPM, secrets, bot, and client setup. Replace example IPs and domains with your own; keep production values in local `.env` only.

Quick install (interactive Docker wizard, v3.1.2):

```bash
# Prefer cloning — installer needs deploy/lib/installer-lib.sh
git clone --branch dev https://github.com/CyberoniOntoni/FamilyGram-Server.git /opt/familygram-server
cd /opt/familygram-server
bash deploy/install.sh          # use ssh -t if prompts don't echo input
```

Or download the script pair:

```bash
curl -fsSL https://raw.githubusercontent.com/CyberoniOntoni/FamilyGram-Server/dev/deploy/install.sh -o install.sh
curl -fsSL https://raw.githubusercontent.com/CyberoniOntoni/FamilyGram-Server/dev/deploy/lib/installer-lib.sh -o installer-lib.sh
sudo bash install.sh
```

Save scripts first — do **not** use `curl ... | bash` (breaks interactive prompts).  
Proxmox LXC: `deploy/install-lxc.sh` is a thin wrapper around the same installer.

Non-interactive example:

```bash
PUBLIC_IP=1.2.3.4 LAN_IP=192.168.1.10 BOT_TOKEN='123456789:AAH...' \
  bash deploy/install.sh --non-interactive --start
```

Run `bash deploy/verify-installer.sh` on the host to sanity-check the installer scripts.

#### Installer prompts — typical choices

| Prompt | Small private server | Notes |
|--------|----------------------|-------|
| Passkey (WebAuthn) | **No** | Needs HTTPS + domain + NPM; not required for normal login |
| RTMP live streaming | **No** | Only for OBS-style live broadcasts |
| Bot token | **Yes** | One `@BotFather` bot serves all users; delivers login codes |
| Customize ports | **No** | Defaults work (`20443`–`20644`, STUN/TURN `5348`) |

Public WAN IP goes into client configs and `.env` (`DcOptions`, WebRTC). LAN IP is only for router port-forward targets — never put the LAN IP in `DcOptions`.

#### Router / firewall port forwards

Notation: `port(PROTO)` — `TCP&UDP` means create **both** TCP and UDP rules to the same WAN port → your host LAN IP.

| WAN port | Required | Service |
|----------|----------|---------|
| `20443(TCP)` | yes | MTProto DC1 (main client entry) |
| `20543(TCP)` | yes | MTProto DC2 |
| `20643(TCP)` | yes | MTProto DC3 |
| `20644(TCP)` | yes | MTProto DC4 (media) |
| `5348(TCP&UDP)` | yes | STUN/TURN (voice/video) |
| `49152-49172(UDP)` | yes | TURN relay media |
| `30443(TCP)` | optional | HTTPS (passkey / web only) |
| `1935(TCP)`, `8888(TCP)` | optional | RTMP live (only if RTMP enabled) |
| **`5005`** | **no** | Internal Docker only (`sms-sender` → `bot`); bot talks **outbound** to Telegram |

MTProto must go **direct** to your public IP — not through Cloudflare orange-cloud or Nginx Proxy Manager.

### Quick Start with Docker

1. Get the Docker Compose setup. `docker-compose.yml` bind-mounts several helper scripts
   (`init-calls.sh`, `init-business.sh`, `init-botfather.sh`, `minio-proxy.conf`, ...) from this directory,
   so clone the repo rather than downloading `docker-compose.yml` on its own:

```bash
git clone --depth 1 https://github.com/CyberoniOntoni/FamilyGram-Server.git
cd familygram-server/docker/compose
cp .env.example .env
```

2. Edit `.env`:
   - Replace `YOUR_SERVER_IP` with your server's public IP address
   - Set strong passwords for `CHANGE_ME` fields (RabbitMQ, Minio, encryption keys)

3. Prepare data directories and start:

```bash
mkdir -p ./data/mytelegram/data-seeder/downloads ./data/mytelegram/data-seeder/logs
mkdir -p ./data/{mongo/db,mongo/configdb,minio,coturn,rtmp,bot,redis,rabbitmq,geoip}
chmod -R a+w ./data
docker compose up -d
```

### Configuration

Key `.env` settings:

| Variable | Description |
|----------|-------------|
| `App__DcOptions__0__IpAddress` | Your server's public IP |
| `RabbitMQ__Connections__Default__Password` | RabbitMQ password |
| `App__AccessHashSecretKey` | Random secret key |
| `App__EncryptionConfig__MessageKeys__0__Key` | Base64 encryption key |
| `App__FixedVerifyCode` | Fixed code for all logins (testing only; leave empty in production) |
| `BOT_TOKEN` | Telegram bot token used to deliver login codes (see [Verification Bot](#verification-bot)) |
| `App__RtmpStreamUrl` / `App__RtmpHlsUrl` | RTMP live streaming (optional; leave empty if disabled) |
| `TwilioSms__*` | Real SMS via Twilio (optional; leave empty when using the bot) |

`BOT_TOKEN` is optional for a first boot — the `bot` container keeps restarting until it's set and does not block
the rest of the stack — but login codes won't reach real users' Telegram accounts until it's configured.

Silence harmless `docker compose` warnings about unset optional variables by adding empty defaults to `.env`:

```bash
App__RtmpStreamUrl=
App__RtmpHlsUrl=
App__FixedVerifyCode=
TwilioSms__Enabled=False
TwilioSms__AccountSId=
TwilioSms__AuthToken=
TwilioSms__FromNumber=
TwilioSms__MessagingServiceSId=
```

### Voice & Video Calls Setup

Voice and video calls **require** a TURN/STUN server. Install Coturn:

```bash
sudo apt-get install coturn
# Configure /etc/turnserver.conf (see docs/CALLS_SETUP.md)
sudo systemctl start coturn
```

Configure WebRTC in `.env`:

```bash
# REQUIRED for calls to work
App__WebRtcConnections__0__Ip=YOUR_SERVER_IP
App__WebRtcConnections__0__Port=5348
App__WebRtcConnections__0__Turn=True
App__WebRtcConnections__0__Stun=True
App__WebRtcConnections__0__UserName=testgram
App__WebRtcConnections__0__Password=testgram123
```

Setup MongoDB indexes (automatic on first start):

```bash
cd scripts && ./setup_call_indexes.sh  # Optional: manual setup
```

See [docs/CALLS_SETUP.md](docs/CALLS_SETUP.md) for complete setup instructions.

## Troubleshooting

### `docker compose` warns about unset `App__Rtmp*` / `TwilioSms__*` / `App__FixedVerifyCode`

These are optional features. Empty values are fine. Add the blank defaults from [Configuration](#configuration) to `.env` if you want the warnings gone.

### `data-seeder` — `Permission denied` on `/app/downloads/dataseeder.json`

The data-seeder container must write under the mounted `downloads` volume:

```bash
cd docker/compose
mkdir -p data/mytelegram/data-seeder/downloads data/mytelegram/data-seeder/logs
chmod -R a+w data/mytelegram/data-seeder
docker compose restart data-seeder
docker compose logs data-seeder | tail -20   # expect: "All data created"
```

### `data-seeder` — language pack file is missing

The host bind-mount hides language packs baked into the image. Copy them once:

```bash
cd docker/compose
cid=$(docker create ghcr.io/cyberoniontoni/familygram-server/mytelegram-data-seeder:latest)
docker cp "$cid:/app/downloads/langpacks" ./data/mytelegram/data-seeder/downloads/
docker rm "$cid"
chmod -R a+w data/mytelegram/data-seeder/downloads
docker compose restart data-seeder messenger-query-server
```

Or copy from the repo:

- `source/src/MyTelegram.DataSeeder/downloads/langpacks/ru/android.json` → `data/.../langpacks/ru/`
- `source/src/MyTelegram.DataSeeder/downloads/langpacks/en/android.json` → `data/.../langpacks/en/` (required for complete **English** UI in FamilyGram Web)

The data-seeder imports **both** Russian and English Android packs into MongoDB for all platforms including `weba`. If English menus show untranslated keys, ensure `en/android.json` is present and restart `data-seeder` + `messenger-query-server`.

### Clients get `ConnectionRefusedError` (connection to server fails)

If clients fail to connect with an error like:

```
Attempt 1 at connecting failed: ConnectionRefusedError: [WinError 1225] The remote computer refused the network connection
```

but the VDS/host itself is reachable, the gateway is most likely not listening on the
main port **20443** (DC1, the first port clients connect to — see `App__DcOptions__0__Port`).

Cause: `App__Servers__0__Enabled` is unset/commented in `.env`. docker-compose always
passes this variable to the gateway container, so an unset value becomes an **empty
string**. An empty value makes .NET drop server 0 from the config entirely, so the
gateway never opens the 20443 listener and every connection is refused.

Fix: make sure `.env` contains an active line (not commented, not empty):

```bash
App__Servers__0__Enabled=True
```

Then recreate the gateway and verify it listens on 20443:

```bash
cd docker/compose
docker compose up -d --force-recreate gateway-server
docker compose logs gateway-server | grep 20443   # expect: "Tcp server started at ...:20443"
```

### file-server spams `Bucket name cannot be empty` / media and verification icons don't load

If `file-server` logs are spammed with:

```
Minio.Exceptions.InvalidBucketNameException: MinIO API responded with message=Bucket name cannot be empty.
```

and avatars, stickers, or custom verification icons fail to load in clients, `Minio__BucketName`
is unset/commented in `.env`. docker-compose always passes this variable to file-server, so an
unset value becomes an empty string, and every file request fails.

Fix: make sure `.env` contains active lines (not commented, not empty):

```bash
Minio__BucketName=tg-files
Minio__CreateBucketIfNotExists=True
```

Then recreate file-server:

```bash
cd docker/compose
docker compose up -d --force-recreate file-server
```

### file-server spams `NullReferenceException` in `MinioStoringHelper.GetAsync` / downloads stall

If `file-server` logs are flooded with:

```
[ERR] Get file failed, input: FileId: "..." Offset: ... Limit: 32768
System.NullReferenceException: Object reference not set to an instance of an object.
   at Minio.MinioClient.ParseWellKnownErrorNoContent(ResponseResult response)
   ...
   at MyTelegram.FileServer.Services.MinioStoringHelper.GetAsync(...)
```

this is a regression in the MinIO .NET SDK bundled inside the upstream
`mytelegram-file-server` image (Minio 6.0.6-local). When MinIO answers a byte-range
request with `416 Range Not Satisfiable` (no body) — which Telegram clients trigger
for the final chunk of a download (offset at/after EOF) — the SDK fails to handle the
416, leaves its error object null, and `throw error;` turns into a NullReferenceException.

Because the file-server is built and published separately, it can't be patched from
this repo. Instead, file-server is routed through the **minio-proxy** service (a tiny
nginx proxy) which rewrites those 416 responses into a clean empty 200 the SDK accepts.
All other traffic passes through untouched.

This is wired up by default (`Minio__FileServerEndpoint` defaults to `minio-proxy:9000`).
If you see this error, make sure the proxy is running and file-server points at it:

```bash
cd docker/compose
docker compose up -d minio-proxy
docker compose up -d --force-recreate file-server
```

## Building Docker Images

### CI (GitHub Actions)

[`.github/workflows/docker-build.yml`](.github/workflows/docker-build.yml) builds the six .NET services this fork
carries source for — `messenger-command-server`, `messenger-query-server`, `gateway-server`, `auth-server`,
`sms-sender`, `data-seeder` — plus the Python verification bot (`familygram-server-bot`), and pushes them to GHCR on every
push to `dev` and on `v*.*.*` tags (pull requests build but don't push). Images are published as:

```
ghcr.io/CyberoniOntoni/FamilyGram-Server/<service-name>:latest
ghcr.io/CyberoniOntoni/FamilyGram-Server/<service-name>:<version>   # from build/version.txt
ghcr.io/CyberoniOntoni/FamilyGram-Server/<service-name>:<git-sha>
```

`docker-compose.yml` already points at these images through `FamilyGramServerRegistry`/`FamilyGramServerVersion` in `.env`
(see `.env.example`), so `docker compose pull && docker compose up -d` picks up whatever CI published. `session-server`
and `file-server` aren't part of this fork's source, so they keep pulling prebuilt images from the upstream
MyTelegram registry via the separate `MyTelegramRegistry`/`MyTelegramVersion` variables.

> GHCR packages are private by default even on a public repo. The first time the workflow runs, make each
> `ghcr.io/CyberoniOntoni/FamilyGram-Server/<service-name>` package public under the repo/org's **Packages** settings,
> or `docker login ghcr.io` with a token that has `read:packages` before pulling.

You can also trigger a build manually from the **Actions** tab (`workflow_dispatch`).

### Local build

`build/docker/*.sh` default to tagging images as `mytelegram/<service-name>`. `docker-compose.yml` pulls
`${FamilyGramServerRegistry}/<service-name>:${FamilyGramServerVersion}` (default `ghcr.io/CyberoniOntoni/FamilyGram-Server`), so set
`REGISTRY_URL` to the same value before building, or `docker compose up -d` will just re-pull from GHCR instead
of using your local build:

```bash
# Linux amd64
cd build/docker
export REGISTRY_URL="ghcr.io/CyberoniOntoni/FamilyGram-Server"   # match FamilyGramServerRegistry in .env
./build-all-amd64.sh

# Linux arm64
export REGISTRY_URL="ghcr.io/CyberoniOntoni/FamilyGram-Server"
./build-all-arm64.sh
```

## Clients

| Platform | Repository |
|----------|------------|
| Android | https://github.com/glebxdlolreal/testgram-android |
| Desktop (FamilyGram) | https://github.com/CyberoniOntoni/familygram-desktop — see [docs/BUILD-testgram.md](https://github.com/CyberoniOntoni/familygram-desktop/blob/main/docs/BUILD-testgram.md) |
| **Web + Server (FamilyGram)** | **https://github.com/CyberoniOntoni/familygram** — unified Docker stack (recommended) |
| Web source only | https://github.com/CyberoniOntoni/familygram-web — telegram-tt fork |
| iOS | https://github.com/loyldg/mytelegram-iOS |
| WebK | https://github.com/loyldg/mytelegram-webk |
| WebA | https://github.com/loyldg/mytelegram-weba (legacy; superseded by familygram-web for Testgram) |

### Configure Clients
1. Clone the client source code.
2. Search for `YOUR_SERVER_IP` in all files and replace it with your own server IP.

## Verification Bot

The repo includes a Telegram bot (`bot/`) that delivers login/verification codes to the Telegram account a user
linked their phone number with (`/start` → add number). One bot serves your whole group — you do not need a bot
per user.

```text
Client login  →  auth.sendCode  →  sms-sender  →  http://bot:5005/send  (Docker internal)
                                                      ↓
                                            Telegram DM with code (outbound to api.telegram.org)

User links phone  →  Telegram app  →  @YourBot  →  /start  (via Telegram cloud, not your router)
```

- **Do not port-forward 5005** — it is only reachable inside the Docker network.
- The bot uses **long polling** (outbound HTTPS to Telegram). Your host only needs general internet access.
- `sms-sender` already points at `http://bot:5005/send` in `docker-compose.yml`.

**Setup:**

1. Create a bot in [@BotFather](https://t.me/BotFather) → `/newbot` → copy the token.
2. Set `BOT_TOKEN=...` in `docker/compose/.env` (or pass it to `deploy/install.sh`).
3. After the stack is up, each user opens **your** bot in Telegram → `/start` → links their phone number.
4. On Testgram client login with that number, the code arrives in the bot chat.

`sms-sender` can optionally also consume `AppCodeCreatedIntegrationEvent` from RabbitMQ
(`ENABLE_RABBITMQ_CONSUMER=true` in the bot service).

**Docker (recommended, already wired into `docker-compose.yml`):**

```bash
# In .env: set BOT_TOKEN (and optionally BOT_TOKEN1, BOT_TOKEN2, ...)
docker compose up -d bot
docker compose logs bot   # expect: "Configured bot @your_bot" and "Bot started on port 5005"
```

**Manual (without Docker):**

```bash
cd bot
cp .env.example .env
# Edit .env with your BOT_TOKEN and RABBITMQ_URL
python3 bot.py
```

## Admin: Give Stars to a User

Connect to MongoDB and run:

```js
// mongosh tg

// 1. Add balance
db['star-transactions'].insertOne({
  UserId: Long('USER_ID'),
  Amount: 1000,          // number of stars
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

> Replace `USER_ID` with the target user ID (find it via `db['eventflow-userreadmodel'].find({UserName: 'username'})`).

---

## Admin: Add Star Gifts

Gifts are stored in the `star-gifts` collection. To add a new gift:

```js
// mongosh tg

db['star-gifts'].insertOne({
  GiftId: Long('UNIQUE_GIFT_ID'),   // unique ID (e.g. 1001)
  Stars: 50,                         // price in stars
  Title: 'My Gift',
  Description: '',
  DocumentId: Long('DOCUMENT_ID'),   // sticker/document ID from Telegram
  LimitedQuantity: 0,                // 0 = unlimited
  SoldCount: 0,
  Available: true,
  FirstSaleDate: new Date(),
  LastSaleDate: null
});
```

To give a gift to a user directly (without purchase):

```js
db['saved-star-gifts'].insertOne({
  UserId: Long('RECIPIENT_USER_ID'),
  FromUserId: Long('0'),
  GiftId: Long('UNIQUE_GIFT_ID'),
  Stars: 50,
  Message: '',
  Saved: true,
  Date: new Date()
});
```

---

## Admin: Add Star Gift Upgrades

To make a gift upgradeable, you need to:

**1. Set upgrade cost on the gift:**
```js
// mongosh tg
db['star-gifts'].updateOne(
  { GiftId: Long('GIFT_ID') },
  { $set: {
    UpgradeStars: 1000,        // stars required to upgrade
    AvailabilityTotal: 10000   // total unique copies that can exist
  }}
);
```

**2. Add upgrade config (attributes for unique version):**

Each unique gift gets 3 attributes: `model` (sticker), `backdrop` (background), `pattern` (overlay).
Add variants to `star-gift-upgrade-config`:

```js
db['star-gift-upgrade-config'].insertMany([
  // Model (sticker variant)
  {
    gift_id: Long('GIFT_ID'),   // 0 = applies to all gifts
    type: 'model',
    name: 'Rare Model',
    rarity_permille: 100,       // 100 = 10% chance (out of 1000)
    document_id: Long('STICKER_DOCUMENT_ID')
  },
  // Backdrop (background colors)
  {
    gift_id: Long('GIFT_ID'),
    type: 'backdrop',
    name: 'Golden',
    rarity_permille: 50,
    backdrop_id: 1,
    center_color: 0xF1C40F,
    edge_color: 0xD4AC0D,
    pattern_color: 0xF9E79F,
    text_color: 0xFFFFFF
  },
  // Pattern (overlay sticker)
  {
    gift_id: Long('GIFT_ID'),
    type: 'pattern',
    name: 'Stars',
    rarity_permille: 200,
    document_id: Long('PATTERN_DOCUMENT_ID')
  }
]);
```

> `rarity_permille` — weight out of 1000 (higher = more common). Use `gift_id: 0` for attributes shared across all gifts.

**3. Force-upgrade a gift for a user (admin):**
```js
// Find the saved gift
db['saved-star-gifts'].findOne({ OwnerUserId: Long('USER_ID'), IsUnique: false });

// Then trigger upgrade via API or set UpgradeStars: 0 to make it free
db['star-gifts'].updateOne(
  { GiftId: Long('GIFT_ID') },
  { $set: { UpgradeStars: 0 } }
);
```

---

## Reaction Seeder

After deploying the server, run the reaction seeder to populate emoji reaction animations:

```bash
cd scripts

# 1. Download reaction files from Telegram (~50MB)
TG_API_ID=your_api_id \
TG_API_HASH=your_api_hash \
TG_PHONE=+1234567890 \
python3 seed_reactions.py --download

# 2. Import files into Minio + MongoDB
MONGO_URL=mongodb://localhost:27017 \
MINIO_ENDPOINT=localhost:9000 \
MINIO_ACCESS_KEY=your_key \
MINIO_SECRET_KEY=your_secret \
python3 seed_reactions.py --import

# 3. Generate the C# handler with real document IDs
MONGO_URL=mongodb://localhost:27017 \
HANDLER_PATH=../source/src/MyTelegram.Messenger/Handlers/LatestLayer/Messages/GetAvailableReactionsHandler.cs \
python3 seed_reactions.py --generate-handler

# 4. Rebuild and redeploy messenger images
cd ../build/docker
export REGISTRY_URL="ghcr.io/CyberoniOntoni/FamilyGram-Server"   # match FamilyGramServerRegistry in .env
bash 1.build-messenger-command-server.sh
bash 2.build-messenger-query-server.sh
cd ../../docker/compose && docker compose down && docker compose up -d
```

> **Note:** Steps 1–3 only need to be done once. The generated handler is committed to the repo so subsequent deploys don't require re-seeding.
