"""Revert Latest-layer wire constructor IDs from 228 back to 224 for session-server compat.

MyTelegram session-server is closed-source and only understands layer-224 constructors.
Until it is rebuilt against FamilyGram Schema layer 228, messenger/query/auth/gateway
must serialize/deserialize with 224 IDs so msg_container and push pipelines work.
"""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "source" / "src" / "MyTelegram.Schema"

# 228 -> 224
REVERT = {
    "7600b9d3": "3ae56482",  # message
    "fef48f62": "545cd15a",  # messages.sendMessage
    "b106e66c": "51e842e1",  # messages.editMessage
    "d49f34c6": "1c32b11c",  # channel
    "a04e8d3a": "e4e0b29d",  # channelFull
    "60fe3294": "96eaa5eb",  # draftMessage
    "966e2dbf": "b8425be9",  # poll
    "b1b8cc83": "31774388",  # user
    "7f6a1e22": "24b524c5",  # channels.joinChannel
    "0ecc2618": "4c2985b6",  # channels.toggleJoinRequest
    "033ed001": "cd64636c",  # connectedBot
    "05f58d0f": "11f812d8",  # contacts.search
    "3fc18057": "9bb2636d",  # inputStorePaymentAuthCode
    "daecc589": "fd426afe",  # messages.composeMessageWithAI
    "a423bb51": "83557dba",  # messages.editInlineBotMessage
    "de91436e": "6c50051c",  # messages.importChatInvite
    "ad0fa15c": "54ae308e",  # messages.saveDraft
    "6126a43c": "4bc6589a",  # messages.searchGlobal
    "1fd6f6c1": "9a8ae1e1",  # pageBlockOrderedList
    "63ca67aa": "25e073fc",  # pageListItemBlocks
    "2f58683c": "b92fb6cd",  # pageListItemText
    "8ff2d5f0": "98dd8936",  # pageListOrderedItemBlocks
    "15031189": "5e068047",  # pageListOrderedItemText
    "7cb34d79": "11dfa986",  # updateBotChatInviteRequester
    "9852d6d2": "c27ac8c7",  # botCommand
    "f8827ebf": "e0955a3c",  # auth.sentCodePaymentRequired
}


def main() -> None:
    updated: list[str] = []
    for path in ROOT.rglob("*.cs"):
        text = path.read_text(encoding="utf-8")
        orig = text
        for new, old in REVERT.items():
            text = re.sub(rf"0x{new}", f"0x{old}", text, flags=re.IGNORECASE)
        if text != orig:
            path.write_text(text, encoding="utf-8")
            updated.append(str(path.relative_to(ROOT)))
    print(f"Reverted wire IDs in {len(updated)} files")
    for f in sorted(updated):
        print(" ", f)


if __name__ == "__main__":
    main()
