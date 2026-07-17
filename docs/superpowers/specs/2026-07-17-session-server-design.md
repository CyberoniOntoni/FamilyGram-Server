# FamilyGram SessionServer design (MVP)

**Status:** implementation started 2026-07-17  
**Goal:** Replace closed `mytelegram/mytelegram-session-server` with open code in FamilyGram-Server.

## MVP scope

- Load auth keys from Mongo `eventflow-authkeyreadmodel` (+ cache from `AuthKeyCreatedIntegrationEvent`)
- Decrypt/encrypt MTProto 2.0 (reuse `IMtpHelper` / `IAesHelper`)
- Handle `ping` / `ping_delay_disconnect` locally
- Unpack `msg_container` / `invokeWithLayer` / `invokeAfterMsg`
- Dispatch RPCs to command/query/upload/download Rabbit events
- Encrypt `DataResultResponseReceivedEvent` / `FileDataResultResponseReceivedEvent` back to gateway
- Persist new auth keys from `AuthKeyCreatedIntegrationEvent`
- Update `UserId` / layer from sign-in / bind events when available

## Non-goals (MVP)

- Perfect layered push conversion
- All edge-case session GC / multi-DC media-only keys
- 100% parity with closed binary

## Cutover

Compose `SessionServerImage` override; default stays closed image until MVP verified.
