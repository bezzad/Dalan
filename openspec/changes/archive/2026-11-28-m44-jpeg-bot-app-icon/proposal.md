# m44: install a bot whose icon is a JPEG

## Why
The owner (2026-11-28): Chrome shows Install on dalanet.ir but not on dalanet.ir/@mofidbot. DevTools says the manifest
has no icon that fits: Chrome takes only PNG, SVG or WebP. Mofid's icon is a 512 by 512 JPEG, the usual type of a bot
photo (`setMyProfilePhoto`), and REL-54 put it in the manifest as it is.

## What changes
REL-54: a JPEG icon goes into the manifest as a small SVG that holds the JPEG (`GET /api/bots/{id}/icon.svg`).
No picture library and no new package: the bytes are not decoded or changed. PNG and WebP stay as they are.
No new setting.
