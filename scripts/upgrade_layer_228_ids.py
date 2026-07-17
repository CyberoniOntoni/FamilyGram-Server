"""Patch MyTelegram.Schema constructor IDs from layer 224 to 228."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "source" / "src" / "MyTelegram.Schema"

# old_hex -> new_hex (lowercase, 8 digits, no 0x)
CHANGES = {
    "3ae56482": "7600b9d3",  # message
    "545cd15a": "fef48f62",  # messages.sendMessage
    "51e842e1": "b106e66c",  # messages.editMessage
    "1c32b11c": "d49f34c6",  # channel
    "e4e0b29d": "a04e8d3a",  # channelFull
    "96eaa5eb": "60fe3294",  # draftMessage
    "b8425be9": "966e2dbf",  # poll
    "31774388": "b1b8cc83",  # user
    "24b524c5": "7f6a1e22",  # channels.joinChannel
    "4c2985b6": "0ecc2618",  # channels.toggleJoinRequest
    "cd64636c": "033ed001",  # connectedBot
    "11f812d8": "05f58d0f",  # contacts.search
    "9bb2636d": "3fc18057",  # inputStorePaymentAuthCode
    "fd426afe": "daecc589",  # messages.composeMessageWithAI
    "83557dba": "a423bb51",  # messages.editInlineBotMessage
    "6c50051c": "de91436e",  # messages.importChatInvite
    "54ae308e": "ad0fa15c",  # messages.saveDraft
    "4bc6589a": "6126a43c",  # messages.searchGlobal
    "9a8ae1e1": "1fd6f6c1",  # pageBlockOrderedList
    "25e073fc": "63ca67aa",  # pageListItemBlocks
    "b92fb6cd": "2f58683c",  # pageListItemText
    "98dd8936": "8ff2d5f0",  # pageListOrderedItemBlocks
    "5e068047": "15031189",  # pageListOrderedItemText
    "11dfa986": "7cb34d79",  # updateBotChatInviteRequester
    "c27ac8c7": "9852d6d2",  # botCommand
    "e0955a3c": "f8827ebf",  # auth.sentCodePaymentRequired
}


def main() -> None:
    updated: list[str] = []
    for path in ROOT.rglob("*.cs"):
        text = path.read_text(encoding="utf-8")
        orig = text
        for old, new in CHANGES.items():
            text = re.sub(rf"0x{old}", f"0x{new}", text, flags=re.IGNORECASE)
        if text != orig:
            path.write_text(text, encoding="utf-8")
            updated.append(str(path.relative_to(ROOT)))

    print(f"Updated {len(updated)} files:")
    for f in sorted(updated):
        print(" ", f)


if __name__ == "__main__":
    main()
