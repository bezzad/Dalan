# Proposal

## Why

The owner (2026-11-24): `+98 914 914 9202` is refused; only `09…` without spaces works. People copy numbers from contacts with `+98`, spaces and dashes.

## What Changes

- CLI-60: before it sends a phone number, the client turns every usual way of writing an Iranian mobile into `09XXXXXXXXX`: `+98`, `0098`, `98` or no `0` at the start; spaces, dashes, dots and brackets; Persian and Arabic digits; invisible direction marks. Other text (an email, a foreign number) only gets Latin digits (CLI-12), so the server still says what is wrong.
- Used on the SMS login, the password login (phone as login name), the development login and the "verify your mobile" card.

## Capabilities

### Modified Capabilities

- `client`: CLI-60 (new).

## Impact

Client only (digits.ts, login.ts, account.ts). No server change: the server keeps its strict check. No new setting.
