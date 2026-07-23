# FamilyGram upstream forks (layer228)

Inventory of MyTelegram / client upstreams FamilyGram relies on, what is forked under [CyberoniOntoni](https://github.com/CyberoniOntoni), and layer228 status.

## Cannot fork (closed source — still used in production compose)

| Component | Image | Notes |
|-----------|-------|--------|
| **session-server** (legacy closed) | `mytelegram/mytelegram-session-server` | Still default in compose. Open MVP: `source/src/MyTelegram.SessionServer` → GHCR `mytelegram-session-server` (opt-in via `SessionServerImage`). |
| **file-server** (legacy) | `mytelegram/mytelegram-file-server` | Replaced by open `MyTelegram.FileServer` in FamilyGram-Server (`source/src/MyTelegram.FileServer`). |

**Wire layer 228** is the product default (`Layers.LayerLatest = 228`) when using the **open** session-server image. Do **not** run the closed Docker Hub session-server with 228 wire — it rejects 228 constructors. See [LAYER_228_UPGRADE.md](./LAYER_228_UPGRADE.md).

`opengram-server/opengram`’s `MyTelegram.SessionServer` is a **different** RabbitMQ worker (session metadata only) — **not** a drop-in for production MTProto session-server.

## Forked (CyberoniOntoni) — `layer228` branch

| Upstream | Fork | Wire layer on `layer228` |
|----------|------|---------------------------|
| glebxdlolreal/testgram → FamilyGram-Server | [FamilyGram-Server](https://github.com/CyberoniOntoni/FamilyGram-Server) | **228** wire (open session) / dual-map 224 handlers |
| Ajaxy/telegram-tt → familygram web | [familygram](https://github.com/CyberoniOntoni/familygram) / [familygram-web](https://github.com/CyberoniOntoni/familygram-web) | **P1:** still 224 until client flip |
| telegramdesktop + desktop-app libs | [familygram-desktop](https://github.com/CyberoniOntoni/familygram-desktop), … | **P1:** still 224 until client flip |
| loyldg/mytelegram-android | [testgram-android](https://github.com/CyberoniOntoni/testgram-android) | **P1:** still 224 |
| loyldg/mytelegram-iOS | [mytelegram-iOS](https://github.com/CyberoniOntoni/mytelegram-iOS) | **P1:** still 224 |
| loyldg/mytelegram-webk | [mytelegram-webk](https://github.com/CyberoniOntoni/mytelegram-webk) | **P1:** still 224 |
| loyldg/mytelegram-tdesktop | [mytelegram-tdesktop](https://github.com/CyberoniOntoni/mytelegram-tdesktop) | **P1:** still 224 |
| loyldg/mytelegram-td | [mytelegram-td](https://github.com/CyberoniOntoni/mytelegram-td) | **P1:** still 224 |
| loyldg/mytelegram-bot-api | [mytelegram-bot-api](https://github.com/CyberoniOntoni/mytelegram-bot-api) | N/A (Bot API) |
| loyldg/mytelegram-weba | [mytelegram-weba](https://github.com/CyberoniOntoni/mytelegram-weba) / [telegram-tt](https://github.com/CyberoniOntoni/telegram-tt) | **P1:** still 224 |
| opengram-server/opengram | [opengram](https://github.com/CyberoniOntoni/opengram) | align with FamilyGram-Server when used |

Each client fork includes `FAMILYGRAM.md` on `layer228` describing the wire policy.

## Client migration (P1)

Server **Latest wire is 228** and serializes Latest constructors on the wire (pushes, RPC results). Dual object-id maps still accept common **224 request** constructors during migration.

Clients still on 224 **must** be updated promptly: they may fail to parse 228 response constructors (`user#b1b8cc83`, `message#7600b9d3`, etc.) once all server images run this release.

**Required together:** open `SessionServerImage` + messenger/* rebuilt from this Schema + client `LAYER = 228` (web: `TG_GRAMJS_LAYER=228`, drop 224 sendMessage force).
