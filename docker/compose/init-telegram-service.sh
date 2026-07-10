#!/bin/bash
set -euo pipefail

echo "MongoDB is ready. Checking Telegram service user (777000)..."

docker compose exec -T mongodb mongosh tg --quiet <<'MONGO'
const serviceUserId = Long(777000);
const now = new Date();
const users = db.getCollection("eventflow-userreadmodel");
const existing = users.findOne({ UserId: serviceUserId });

if (existing) {
    print("Telegram service user already exists with UserId: " + serviceUserId);
} else {
    print("Creating Telegram service user...");
    users.insertOne({
        "_id": "user-service-telegram",
        "About": "Testgram service notifications",
        "AccessHash": Long(Math.floor(Math.random() * 9007199254740991)),
        "AccountTtl": 365,
        "Birthday": null,
        "Bot": false,
        "BotActiveUsers": null,
        "BotHasMainApp": false,
        "BotInfoVersion": 0,
        "BotChatHistory": false,
        "BotNochats": false,
        "Color": { "Color": 0, "BackgroundEmojiId": null },
        "CreationTime": now,
        "Email": null,
        "EmojiStatusDocumentId": null,
        "EmojiStatusValidUntil": null,
        "FallbackPhotoId": null,
        "FirstName": "Telegram",
        "GlobalPrivacySettings": {
            "ArchiveAndMuteNewNoncontactPeers": false,
            "KeepArchivedUnmuted": false,
            "KeepArchivedFolders": false,
            "HideReadMarks": false,
            "NewNoncontactPeersRequirePremium": false,
            "NoncontactPeersPaidStars": null,
            "DisallowUnlimitedStargifts": false,
            "DisallowLimitedStargifts": false,
            "DisallowUniqueStargifts": false,
            "DisallowPremiumGifts": false
        },
        "HasPassword": false,
        "IsOnline": false,
        "LastName": "",
        "LastUpdateDate": now,
        "PersonalChannelId": null,
        "PersonalPhotoId": null,
        "PhoneNumber": "42777",
        "PinnedMsgId": null,
        "PinnedMsgIdList": [],
        "Premium": false,
        "ProfileColor": null,
        "ProfilePhoto": null,
        "ProfilePhotoId": null,
        "ProfilePhotoUpdateDate": null,
        "RecentEmojiStatuses": [],
        "SensitiveCanChange": false,
        "SensitiveEnabled": false,
        "ShowContactSignUpNotification": false,
        "Fake": false,
        "Scam": false,
        "Support": true,
        "UserId": serviceUserId,
        "UserName": null,
        "Usernames": null,
        "UserNameUpdateDate": null,
        "IsDeleted": false,
        "Verified": true,
        "Version": Long(1),
        "VideoEmojiMarkup": null,
        "DcId": 1
    });
    print("Telegram service user created successfully with UserId: " + serviceUserId);
}
MONGO

echo "Telegram service user setup complete."