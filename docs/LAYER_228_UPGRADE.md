# API Layer 228 upgrade

**Status:** complete for production — `Layers.LayerLatest = 228` with open session-server  
**Production wire layer:** **228** only (responses, pushes, `invokeWithLayer`)  
**Not multi-layer:** clients should use layer **228**. Dual object-id registration only *accepts* a few common **224 request** constructors so lagging clients can migrate; those shims will be removed when no 224 clients remain.  
**Upstream forks:** see [UPSTREAM_FORKS.md](./UPSTREAM_FORKS.md)

## What landed (P0)

| Area | Change |
|------|--------|
| `Layers.LayerLatest` | **228** |
| Schema constructor IDs | 26 ID-changed types at 228 wire IDs (`scripts/upgrade_layer_228_ids.py`) |
| Dual object-id map | 224 + 228 IDs still registered for send/edit/draft/join/import (clients can migrate) |
| Open session-server | Uses shared Schema — must redeploy with this change |
| Vendored schema | `docs/api.tl.228` |

## Client follow-up (P1 — not this commit)

| Client | Action |
|--------|--------|
| familygram web | `TG_GRAMJS_LAYER=228`; remove 224 force on `sendMessage` / `familygramTlCompat` |
| Desktop / Android / iOS / webk | Raise `LAYER` / `MTPROTO_LAYER` to 228 |

## Still remaining (API surface, not wire)

| Namespace | Methods | Status |
|-----------|---------|--------|
| `communities.*` | create, getJoinedCommunities, peer links, … | not implemented |
| `ephemeral.*` | sendMessage, deleteMessage, … | not implemented |
| `messages.composeRichMessageWithAI` / `translateRichMessage` | AI rich compose | not implemented |
| `bots.*` access settings / join chat results | bot guest chat | not implemented |
| `stats.getPollStats` | poll stats | not implemented |
| Full AI compose backend | real LLM tones | stubs only |

## Acceptance

- [x] Server Latest = 228  
- [x] Schema wire IDs at 228 (with dual map for key handlers)  
- [x] Open session-server builds against Schema  
- [ ] Lab smoke: invokeWithLayer 228 + sendMessage#fef48f62  
- [ ] Web + other clients flipped to 228  
- [ ] Full new API surface  
- [ ] Drop dual-ID shims when no 224 clients remain  

## Do not

- Deploy messenger/session with 228 Schema while clients still force 224-only *responses* that the new Schema no longer emit as Latest (push uses Latest IDs).  
- Mix closed Docker Hub session-server with 228 wire (closed image cannot deserialize 228 constructors).  
- Ship only a constant bump without Schema ID upgrade.

## Rollback

```bash
python scripts/revert_layer_228_wire_ids.py
# set Layers.LayerLatest = 224
# rebuild + redeploy session + messenger*
```
