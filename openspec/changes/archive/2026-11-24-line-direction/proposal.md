# Proposal

## Why

The owner (2026-11-24, screenshot of ArcaneBot): a message whose first line is Latin (`🧵 Dalan [a37dcb87] · sms-ir`) shows all its Persian lines left to right, with the dots and brackets in the wrong place. The whole text takes one direction from its first strong letter. Telegram takes the direction of each line on its own.

## What Changes

- CLI-58: each line of a message's text takes its direction from its own first strong letter (Persian, Arabic or Hebrew: right to left; Latin: left to right) and lines up at its own start. Blockquotes too. Code blocks stay left to right. No language detection: the Unicode bidi rule (`unicode-bidi: plaintext`).

## Capabilities

### Modified Capabilities

- `client`: CLI-58 (new).

## Impact

Client only: app.css. No new setting.
