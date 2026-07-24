# FamilyGram upstream forks

Inventory of MyTelegram / client upstreams FamilyGram relies on and what is forked under [CyberoniOntoni](https://github.com/CyberoniOntoni).

**Default branch for FamilyGram-Server and the unified stack is `main`.** Wire layer **228** is the product default.

## Server components (this monorepo / GHCR)

| Component | Image (GHCR) | Notes |
|-----------|--------------|--------|
| **session-server** (open) | `ghcr.io/cyberoniontoni/familygram-server/mytelegram-session-server:latest` | Built from `source/src/MyTelegram.SessionServer`. Required for layer **228** wire. |
| **file-server** (open) | `ghcr.io/cyberoniontoni/familygram-server/mytelegram-file-server:latest` | Built from `source/src/MyTelegram.FileServer`. |
| messenger / gateway / auth / … | `ghcr.io/cyberoniontoni/familygram-server/mytelegram-*:latest` | CI on **`main`** publishes `:latest`. |

Legacy closed Docker Hub images (`mytelegram/mytelegram-session-server`, `mytelegram/mytelegram-file-server`) are **not** used by current FamilyGram compose defaults. Do **not** pair the closed session-server with 228 wire — it rejects 228 constructors. See [LAYER_228_UPGRADE.md](./LAYER_228_UPGRADE.md).

`opengram`’s session worker is a **different** RabbitMQ service (session metadata only) — **not** a drop-in for production MTProto session-server.

## Forked clients (CyberoniOntoni)

| Upstream | Fork | Wire layer |
|----------|------|------------|
| glebxdlolreal/testgram → FamilyGram-Server | [FamilyGram-Server](https://github.com/CyberoniOntoni/FamilyGram-Server) | **228** (open session); dual-map 224 request accept |
| Ajaxy/telegram-tt | [familygram](https://github.com/CyberoniOntoni/familygram) `web/` | **228** (`TG_GRAMJS_LAYER=228`) |
| telegramdesktop + libs | [familygram-desktop](https://github.com/CyberoniOntoni/familygram-desktop) | migrate to 228 |
| loyldg/mytelegram-android | [testgram-android](https://github.com/CyberoniOntoni/testgram-android) | migrate to 228 |
| loyldg/mytelegram-iOS | [mytelegram-iOS](https://github.com/CyberoniOntoni/mytelegram-iOS) | migrate to 228 |
| loyldg/mytelegram-webk | [mytelegram-webk](https://github.com/CyberoniOntoni/mytelegram-webk) | migrate to 228 |
| loyldg/mytelegram-td | [mytelegram-td](https://github.com/CyberoniOntoni/mytelegram-td) | migrate to 228 |
| loyldg/mytelegram-bot-api | [mytelegram-bot-api](https://github.com/CyberoniOntoni/mytelegram-bot-api) | N/A (Bot API) |

## Client migration

Server **Latest wire is 228** and serializes Latest constructors on the wire (pushes, RPC results). Dual object-id maps still accept common **224 request** constructors during migration.

Clients still on 224 should be updated: they may fail to parse 228 response constructors once all server images run this release.

**Required together:** open `SessionServerImage` + messenger images from FamilyGram-Server `main` + client `LAYER = 228` (web: `TG_GRAMJS_LAYER=228`).
