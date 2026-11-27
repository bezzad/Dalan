# Proposal

## Why

In Telegram, typing `/` in a bot's chat opens a list of the bot's commands, and `/tic` narrows it to `/ticket`. Dalan only has the ☰ command menu (CLI-15); a user who types `/` gets no help and must know each command (owner, 2026-11-25).

## What Changes

1. **Command suggestions (CLI-74).** When the input starts with `/` and has no space yet, a list above the input shows the bot's commands (REL-26) whose name starts with what was typed after `/` (no case), in the bot's order, each with `/command` and its description.
2. **Choose.** A click or tap, or Enter on the marked row, sends `/command` at once (like CLI-15). Tab puts `/command ` in the input to add words after it. ↑ and ↓ move the mark; Escape closes the list.
3. The list closes when nothing matches, after a space, when the input is empty, and when the message is sent. Commands read left to right also on a Persian page. No list when the bot has no commands.

Out of scope: matching inside a name (only the start, like Telegram), commands with `@botname`.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client`: new CLI-74.

## Impact

- Client: `chat.ts` (the list; the commands CLI-15 already loads), a pure function `suggest(commands, input)` with tests, `app.css`, `docs/UI.md` chat section.
- No server change; no new settings (the list shows every match; at most `Relay:MaxCommands` exist, and it scrolls).
