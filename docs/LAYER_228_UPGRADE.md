# API Layer 228 upgrade

**Status:** implemented (core dual-layer path)  
**Production target layer:** **228** (`Layers.LayerLatest`)  
**Min supported:** **224** (request dual-registration + legacy constructor aliases)

## What landed

| Area | Change |
|------|--------|
| `Layers.LayerLatest` | **228** |
| Schema constructor IDs | 26 ID-changed types updated to 228 (message, user, channel, sendMessage, …) |
| New optional fields | `rich_message`, `guestchat_via_from`, community/guard/bot flags, poll countries, … |
| New types | `RichMessage` / `InputRichMessage` / `InputRichFile*`, `messages.ChatInviteJoinResult*` |
| Deserialize aliases | `SerializerObjectMappings.LegacyToLatestConstructorIds` maps 224 → 228 types |
| Handler dual-register | `HandlerHelper` also binds 224 request IDs to Latest handlers |
| Web | `LAYER = 228`; `familygramTlCompat` no longer rewrites constructors |

Vendored schema reference: `docs/api.tl.228` (from tdesktop `// LAYER 228`).

## What still remains

1. **Full codegen** of ~120 *new* methods/constructors (`communities.*`, `ephemeral.*`, `aicompose.*`, web-browser settings, …) — currently unhandled → `NotImplementedException` / unknown objectId.
2. **Structural page-list / botCommand** multi-layer response converters for pure-224 clients (server emits 228 wire forms).
3. **`channels.joinChannel` / `messages.importChatInvite` return type** is still `Updates` on the handler path (async saga). Layer 228 expects `messages.ChatInviteJoinResult`; wrap saga RPC replies when that path is next touched.
4. **CI image tags** `0.xx.228.y` and staging QA matrix.

## Acceptance (core)

- [x] Server advertises / implements Latest = 228  
- [x] `messages.sendMessage` accepts **both** `#545cd15a` (224) and `#fef48f62` (228)  
- [x] History messages serialize as `message#7600b9d3`  
- [x] Web default `LAYER = 228`  
- [ ] Full new API surface stubs  
- [ ] Join/import return-type wrap for 228  
- [ ] Production deploy + regression suite green  

## Do not

- Ship only a constant bump without dual-ID registration (done together here).  
- Force Web back to 224 while Latest is 228 without re-enabling compat patches.
