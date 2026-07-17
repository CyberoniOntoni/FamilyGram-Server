# FamilyGram upstream forks (layer228)

Inventory of MyTelegram / client upstreams FamilyGram relies on, what is forked under [CyberoniOntoni](https://github.com/CyberoniOntoni), and layer228 status.

## Cannot fork (closed source — still used in production compose)

| Component | Image | Notes |
|-----------|-------|--------|
| **session-server** (legacy closed) | `mytelegram/mytelegram-session-server` | Still default in compose. Open MVP: `source/src/MyTelegram.SessionServer` → GHCR `mytelegram-session-server` (opt-in via `SessionServerImage`). |
| **file-server** (legacy) | `mytelegram/mytelegram-file-server` | Replaced by open `MyTelegram.FileServer` in FamilyGram-Server (`source/src/MyTelegram.FileServer`). |

Until these are reimplemented or Pro sources obtained, FamilyGram-Server keeps **wire layer 224** (`Layers.LayerLatest = 224`, `LayerTarget = 228`). See [LAYER_228_UPGRADE.md](./LAYER_228_UPGRADE.md).

`opengram-server/opengram`’s `MyTelegram.SessionServer` is a **different** RabbitMQ worker (session metadata only) — **not** a drop-in for production MTProto session-server.

## Forked (CyberoniOntoni) — `layer228` branch

| Upstream | Fork | Wire layer on `layer228` |
|----------|------|---------------------------|
| glebxdlolreal/testgram → FamilyGram-Server | [FamilyGram-Server](https://github.com/CyberoniOntoni/FamilyGram-Server) | 224 wire / 228 target |
| Ajaxy/telegram-tt → familygram web | [familygram](https://github.com/CyberoniOntoni/familygram) / [familygram-web](https://github.com/CyberoniOntoni/familygram-web) | 224 |
| telegramdesktop + desktop-app libs | [familygram-desktop](https://github.com/CyberoniOntoni/familygram-desktop), [lib_base](https://github.com/CyberoniOntoni/lib_base), [lib_ui](https://github.com/CyberoniOntoni/lib_ui), [lib_spellcheck](https://github.com/CyberoniOntoni/lib_spellcheck), [codegen](https://github.com/CyberoniOntoni/codegen), [cmake_helpers](https://github.com/CyberoniOntoni/cmake_helpers) | 224 scheme |
| loyldg/mytelegram-android | [testgram-android](https://github.com/CyberoniOntoni/testgram-android) (also `mytelegram-android`) | 224 (`TLRPC.LAYER`) |
| loyldg/mytelegram-iOS | [mytelegram-iOS](https://github.com/CyberoniOntoni/mytelegram-iOS) | 224 (`MTPROTO_LAYER`) |
| loyldg/mytelegram-webk | [mytelegram-webk](https://github.com/CyberoniOntoni/mytelegram-webk) | 224 (was 223) |
| loyldg/mytelegram-tdesktop | [mytelegram-tdesktop](https://github.com/CyberoniOntoni/mytelegram-tdesktop) | 224 |
| loyldg/mytelegram-td | [mytelegram-td](https://github.com/CyberoniOntoni/mytelegram-td) | 224 |
| loyldg/mytelegram-bot-api | [mytelegram-bot-api](https://github.com/CyberoniOntoni/mytelegram-bot-api) | N/A (Bot API) |
| loyldg/mytelegram-weba | [mytelegram-weba](https://github.com/CyberoniOntoni/mytelegram-weba) / [telegram-tt](https://github.com/CyberoniOntoni/telegram-tt) | 224 |
| opengram-server/opengram | [opengram](https://github.com/CyberoniOntoni/opengram) | 224 wire / 228 target (`Layers.cs`) |

Each client fork includes `FAMILYGRAM.md` on `layer228` describing the wire policy.

## Why clients stay on wire 224

FamilyGram-Server dual-registers some layer-228 handlers and schema, but the **closed session-server** only understands layer-224 constructors. Clients that announce or serialize true layer-228 request types fail at session-server before messenger handlers run.

When a FamilyGram-owned session-server (built against FamilyGram Schema) ships, raise `Layers.LayerLatest` and client `LAYER` / `MTPROTO_LAYER` to **228** together.
