# API Layer 228 upgrade

**Status:** core dual-layer + high-impact web methods  
**Production target layer:** **228** (`Layers.LayerLatest`)  
**Min supported:** **224** (request dual-registration + legacy constructor aliases)

## What landed

| Area | Change |
|------|--------|
| `Layers.LayerLatest` | **228** |
| Schema constructor IDs | 26 ID-changed types updated to 228 |
| New optional fields | `rich_message`, `guestchat_via_from`, community/guard flags, poll countries, … |
| Rich message types | `RichMessage` / `InputRichMessage*` / `InputRichFile*` |
| Join result types | `messages.ChatInviteJoinResult*` + **RPC wrap** for layer ≥ 228 |
| Dual-layer plumbing | legacy constructor aliases + dual handler registration |
| `messages.getRichMessage` | Handler (reuses message store) |
| `aicompose.*` | Schema + stubs (empty tones / synthetic create) |
| `account.get/updateWebBrowserSettings` | Schema + default empty settings |

Vendored schema: `docs/api.tl.228`.

## Still remaining (lower priority / not used by FamilyGram Web core)

| Namespace | Methods | Status |
|-----------|---------|--------|
| `communities.*` | create, getJoinedCommunities, peer links, … | not implemented |
| `ephemeral.*` | sendMessage, deleteMessage, … | not implemented |
| `messages.composeRichMessageWithAI` / `translateRichMessage` | AI rich compose | not implemented |
| `bots.*` access settings / join chat results | bot guest chat | not implemented |
| `stats.getPollStats` | poll stats | not implemented |
| Full AI compose backend | real LLM tones | stubs only |

Unknown methods still fail as unsupported objectId / `NotImplementedException`.

## Acceptance

- [x] Server Latest = 228  
- [x] Dual `messages.sendMessage` constructor IDs  
- [x] History `message#7600b9d3`  
- [x] Web `LAYER = 228`  
- [x] Join/import RPC returns `chatInviteJoinResultOk` for layer 228  
- [x] `messages.getRichMessage` / `aicompose.getTones` / web browser settings respond  
- [ ] Full new API surface  
- [ ] Production merge from `layer228` branch after QA  

## Do not

- Ship only a constant bump without dual-ID registration.  
- Force Web back to 224 while Latest is 228 without re-enabling compat patches.
