# TODO

Backlog of Telegram API surfaces that are still stubbed out (`throw new NotImplementedException()` or an
empty/placeholder response — see the "What Counts as Not Implemented" rule in `CLAUDE.md`). Grouped by system so
work can be picked up incrementally. Counts are handler-file counts under
`source/src/MyTelegram.Messenger/Handlers/LatestLayer/`, not RPC-method counts (a few handlers cover >1 method).

Per `CLAUDE.md`'s **NO STUBS RULE**: don't fill any of these in with a placeholder just to make them "not throw" —
either implement the real logic or leave the `NotImplementedException` until it can be done properly.

## Done this pass
- `auth.resendCode` / `auth.cancelCode` — implemented (new `ResendCode` aggregate method + `AppCodeResentEvent` on
  `AppCodeAggregate`, wired through `SendAppCodeEventHandler` to the existing SMS/bot delivery pipeline).

## Secret Chats (End-to-End Encryption) — ~13 handlers
`RequestEncryptionHandler`, `AcceptEncryptionHandler`, `DiscardEncryptionHandler`, `SendEncryptedHandler`,
`SendEncryptedFileHandler`, `SendEncryptedServiceHandler`, `UploadEncryptedFileHandler`,
`ReadEncryptedHistoryHandler`, `SetEncryptedTypingHandler`, `SendScreenshotNotificationHandler`, etc.
Whole subsystem (already listed under "Soon..." in `CLAUDE.md`): needs its own aggregate for secret chat sessions,
MTProto key-exchange flow (`messages.requestEncryption` → `acceptEncryption`), and encrypted-message storage. Biggest
single chunk of remaining work; do as one project, not handler-by-handler.

## Bot Mini Apps / WebViews / Inline Bots — ~20 handlers
`RequestSimpleWebViewHandler`, `RequestMainWebViewHandler`, `RequestAppWebViewHandler`, `ProlongWebViewHandler`,
`SendWebViewDataHandler`, `SendWebViewResultMessageHandler`, `GetBotAppHandler`, `GetAttachMenuBotHandler`,
`ToggleBotInAttachMenuHandler`, `GetInlineBotResultsHandler`, `SendInlineBotResultHandler`,
`SetInlineBotResultsHandler`, `GetPreparedInlineMessageHandler`, `SavePreparedInlineMessageHandler`,
`SetBotCallbackAnswerHandler`, `SetGameScoreHandler` / `SetInlineGameScoreHandler` / `GetGameHighScoresHandler` /
`GetInlineGameHighScoresHandler`, `CreateBotHandler`, `ExportBotTokenHandler`,
`RequestWebViewButtonHandler`/`GetRequestedWebViewButtonHandler`, `CheckUsernameHandler` (Bots).
Needs a bot-API-facing HTTP layer (mini apps are served over HTTPS, not MTProto) plus inline-query round-tripping to
bots. Games (`SetGameScoreHandler` etc.) are a Telegram-specific legacy feature, lower priority.

## Channel/Group Administration — ~19 handlers
`EditAdminHandler`, `EditBannedHandler`, `GetParticipantHandler`, `GetParticipantsHandler`,
`InviteToChannelHandler`, `JoinChannelHandler`, `LeaveChannelHandler`, `DeleteMessagesHandler`,
`DeleteHistoryHandler`, `DeleteParticipantHistoryHandler`, `EditPhotoHandler`, `EditTitleHandler`,
`GetChannelsHandler`, `GetFullChannelHandler`, `MigrateChatHandler`, `SetDiscussionGroupHandler`,
`ToggleSlowModeHandler`, `ToggleSignaturesHandler`, `TogglePreHistoryHiddenHandler`,
`ToggleParticipantsHiddenHandler`, `UpdateUsernameHandler` (Channels), plus basic-group equivalents in `Messages/`
(`AddChatUserHandler`, `DeleteChatUserHandler`, `EditChatTitleHandler`, `EditChatPhotoHandler`,
`EditChatAboutHandler`, `ExportChatInviteHandler`, `GetExportedChatInviteHandler`, `EditExportedChatInviteHandler`,
`GetAdminsWithInvitesHandler`, `GetChatsHandler`, `GetFullChatHandler`, `GetOnlinesHandler`). This is core
group/channel management — no external infra needed, straightforward CQRS work following existing aggregates
(`ChannelAggregate` already has admin-rights checks per `ChannelAdminRightsChecker`), just needs the handlers wired
up. Good next target after secret chats.

## Payments & Telegram Stars — ~12 handlers
`SendPaymentFormHandler`, `SendStarsFormHandler`, `ExportInvoiceHandler`, `GetPaymentReceiptHandler`,
`AssignAppStoreTransactionHandler`, `RefundStarsChargeHandler`, `ChangeStarsSubscriptionHandler`,
`FulfillStarsSubscriptionHandler`, `BotCancelStarsSubscriptionHandler`, `GetStarGiftWithdrawalUrlHandler`
(NFT/TON withdrawal), `GetBankCardDataHandler`, `ToggleChatStarGiftNotificationsHandler`. Real payment processing
needs a payment provider integration (Stripe is already partially wired for star purchases per `CLAUDE.md`); the
App Store/Play Store transaction handlers need those platforms' receipt-validation APIs, and the TON withdrawal
needs blockchain infra. Flag before implementing — likely candidates for "requires infrastructure, skip" per the
NO STUBS rule unless a provider is chosen.

## Statistics — 7 handlers
`GetBroadcastStatsHandler`, `GetMegagroupStatsHandler`, `GetMessageStatsHandler`, `GetMessagePublicForwardsHandler`,
`GetStoryStatsHandler`, `GetStoryPublicForwardsHandler`, `LoadAsyncGraphHandler`. Needs an aggregation pipeline over
message/view/forward events (probably a MongoDB aggregation job or a separate read model) — no external infra, but
non-trivial data modeling.

## Chat History Import — 5 handlers
`CheckHistoryImportHandler`, `CheckHistoryImportPeerHandler`, `InitHistoryImportHandler`,
`StartHistoryImportHandler`, `UploadImportedMediaHandler`. Needs parsing of third-party export formats (WhatsApp,
etc.) — self-contained, no external infra, but a fair amount of format-specific parsing work.

## SMS Jobs (Peer-to-Peer Login Program) — 7 handlers
`smsjobs.*` (`JoinHandler`, `LeaveHandler`, `IsEligibleToJoinHandler`, `GetStatusHandler`, `GetSmsJobHandler`,
`FinishJobHandler`, `UpdateSettingsHandler`). Official-clients-only niche feature; low priority.

## Telegram Passport / Takeout — 6 handlers
`GetAuthorizationFormHandler`, `SaveSecureValueHandler`, `GetPassportConfigHandler`, `GetTmpPasswordHandler`,
`InitTakeoutSessionHandler`, `GetSavedHandler` (Contacts takeout). Passport needs secure encrypted-document storage
(identity docs) — treat as sensitive-data infra, don't stub. Takeout is a GDPR-style data export flow.

## Remaining Auth handlers — 6
`BindTempAuthKeyHandler` (PFS temp-key binding — used by real clients, worth prioritizing),
`ImportLoginTokenHandler` (QR-login DC redirect), `ImportWebTokenAuthorizationHandler`,
`DropTempAuthKeysHandler`, `CheckPaidAuthHandler`, `ResetLoginEmailHandler` (needs the email-sender infra already
scaffolded via `EmailSenderOptions`).

## Misc
- **Help**: `GetDeepLinkInfoHandler`, `GetUserInfoHandler`/`EditUserInfoHandler` (TSF-internal, low priority),
  `GetPassportConfigHandler` (see Passport above).
- **Contacts**: `AddContactHandler`, `ExportContactTokenHandler`, `ImportContactTokenHandler`, `GetSavedHandler`.
- **Account**: `UpdateNotifySettingsHandler` (real one, worth prioritizing — per-peer notification muting is a
  commonly used client feature), `SendConfirmPhoneCodeHandler` (account-deletion cancellation code — same AppCode
  pattern as `auth.resendCode`, should be quick now that the pattern exists).
- **Updates**: `GetChannelDifferenceHandler` — core channel sync method, likely needed for large channels/many
  unread messages; worth checking client impact before deprioritizing.
- **Messages**: `EditFactCheckHandler`/`GetFactCheckHandler`, `GetExtendedMediaHandler` (paid media), poll-related
  (`AddPollAnswerHandler`/`DeletePollAnswerHandler`/`ReadPollVotesHandler`/`GetUnreadPollVotesHandler`),
  `TogglePeerTranslationsHandler`, `UpdateDialogFilterHandler`, `GetDocumentByHashHandler`,
  `GetOldFeaturedStickersHandler`, URL-auth (`RequestUrlAuthHandler`/`AcceptUrlAuthHandler`/`DeclineUrlAuthHandler`),
  `ComposeMessageWithAIHandler` (no obvious AI backend to call — flag before implementing).

## Suggested order
1. Account/Auth quick wins that reuse the `ResendCode` pattern (`SendConfirmPhoneCodeHandler`).
2. Channel/Group administration (self-contained, no infra, high user-facing value).
3. `BindTempAuthKeyHandler` / `GetChannelDifferenceHandler` (used by real clients under normal load).
4. Secret chats (biggest scope, but a named "Soon" feature).
5. Everything needing external infra (Payments beyond Stripe, Passport, App Store receipts, WebViews) — confirm
   which providers/infra are actually available before starting, per the NO STUBS rule.
