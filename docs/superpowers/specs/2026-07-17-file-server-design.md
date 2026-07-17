# FamilyGram FileServer design (MVP)

**Status:** approved 2026-07-17  
**Goal:** Replace closed `mytelegram/mytelegram-file-server` with open `MyTelegram.FileServer` in FamilyGram-Server.

## Scope (MVP)

- MinIO object storage + Mongo part/metadata
- MTProto lane: `upload.saveFilePart`, `upload.saveBigFilePart`, `upload.getFile`
- gRPC: `SaveMedia`, `SavePhoto`, `Exists`
- Docker image + CI + compose switch to GHCR

## Non-goals

- Full sticker/encrypted parity, CDN, multi-DC

## Architecture

Query-server `FileDownloadLaneRouter` → exchange `mytelegram_file_server_exchange` → queue `MyTelegramFileServerRaw` → FileServer.

Responses: `FileDataResultResponseReceivedEvent` / `DataResultResponseReceivedEvent` on `mytelegram_exchange` for session-server.

Messenger → gRPC `MediaService` @ `http://file-server:8080`.

## Object keys

`files/{fileId}` for assembled content; parts in Mongo `file_parts` then promoted to MinIO on SavePhoto/SaveMedia.
