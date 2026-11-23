# Proposal

## Why

The owner (2026-11-23): a click on the pinned bar does nothing. It should take the user to the pinned message in the chat.

## What Changes

- CLI-14: the pinned bar is a button. A click closes the search, loads older pages until the message is there, scrolls to it and marks it for a moment. A message not in the history gives a short note.

## Capabilities

### Modified Capabilities

- `client`: CLI-14.

## Impact

Client only: messages.ts (find a message), chat.ts, chatHeader.ts (close the search), app.css, i18n.
