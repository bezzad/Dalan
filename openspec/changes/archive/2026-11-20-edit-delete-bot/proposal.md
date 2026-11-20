# Proposal

## Why

The owner (2026-11-20) created a bot and could not change or remove it.

## What Changes

- REL-48: rename a bot (display name only; the username stays).
- REL-49: delete a bot at once for its owner and users: token stops, gone from lists and the showcase. Its chats stay for the admin until the normal history expiry. The username is free again; chats belong to the bot id, so a new bot with the same name never sees old chats (owner, 2026-11-20).
- CLI-24: Rename and Delete on each bot in "My bots"; Delete asks first; both end in a toast.

## Capabilities

### New Capabilities

- `relay`: REL-48, REL-49.

### Modified Capabilities

- `client`: CLI-24.

## Impact

Relay and client. Migration: `relay.bots.deleted_at`, and the unique username index covers only bots that are not deleted. No new setting.
