# API Layer 228

**Production wire layer:** **228** only (`Layers.LayerLatest` = `Layers.LayerMinSupported` = 228).

There is **no** dual object-id map and **no** multi-layer 224–228 product mode. Clients must negotiate and speak **layer 228** constructors.

## Server

| Item | Value |
|------|--------|
| `Layers.LayerLatest` | **228** |
| `Layers.LayerMinSupported` | **228** |
| Schema constructor IDs | Layer 228 wire IDs only (LayerN types not registered for deserialize) |
| Handlers | Latest-layer only (LayerN forwarders not registered) |
| Responses | Always serialized with Latest converters (`LayeredService` ignores lower client layers) |
| Session / file server | Open GHCR images from FamilyGram-Server `main` |
| Vendored schema | `docs/api.tl.228` |

## Clients

| Client | Requirement |
|--------|-------------|
| familygram web | `TG_GRAMJS_LAYER=228`; no 224 constructor aliases |
| Desktop / Android / iOS / webk | `LAYER` / `MTPROTO_LAYER` = **228** |

## Do not

- Mix closed Docker Hub `mytelegram-session-server` with this Schema (closed image cannot deserialize 228 constructors).
- Re-introduce dual registration of 224 request IDs for “compat”.

## Historical scripts

```bash
# Only if reverting Schema IDs for research (not production)
python scripts/revert_layer_228_wire_ids.py
python scripts/upgrade_layer_228_ids.py
```
