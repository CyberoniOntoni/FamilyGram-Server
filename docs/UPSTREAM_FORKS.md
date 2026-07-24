# FamilyGram upstream forks

Inventory of MyTelegram / client upstreams FamilyGram relies on and what is forked under [CyberoniOntoni](https://github.com/CyberoniOntoni).

**Default branch for FamilyGram-Server and the unified stack is `main`.** Wire layer is **228 only**.

## Server components (this monorepo / GHCR)

| Component | Image (GHCR) | Notes |
|-----------|--------------|--------|
| **session-server** (open) | `ghcr.io/cyberoniontoni/familygram-server/mytelegram-session-server:latest` | Built from `source/src/MyTelegram.SessionServer`. Required for layer **228** wire. |
| **file-server** (open) | `ghcr.io/cyberoniontoni/familygram-server/mytelegram-file-server:latest` | Built from `source/src/MyTelegram.FileServer`. |
| messenger / gateway / auth / … | `ghcr.io/cyberoniontoni/familygram-server/mytelegram-*:latest` | CI on **`main`** publishes `:latest`. |

Legacy closed Docker Hub images are **not** used by current FamilyGram compose defaults.

## Forked clients (CyberoniOntoni)

| Upstream | Fork | Wire layer |
|----------|------|------------|
| FamilyGram-Server | [FamilyGram-Server](https://github.com/CyberoniOntoni/FamilyGram-Server) | **228** |
| familygram web | [familygram](https://github.com/CyberoniOntoni/familygram) `web/` | **228** |
| Desktop / mobile / webk / td | respective forks | must use **228** |

## Client requirement

Server serializes and deserializes **228** constructors only. Clients must set `LAYER = 228` (web: `TG_GRAMJS_LAYER=228`). Older layer constructors are not accepted.
