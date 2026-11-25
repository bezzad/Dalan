# Proposal

## Why

Owner request of 2026-11-25: one person often has two accounts, for example one made by email or GitHub and one made by SMS. Today a phone of another account gets `phone_taken` and the person is stuck. There is also no way to add an email or GitHub to an account.

Owner rules: the person proves both accounts are theirs; a merge never moves or changes a product (bot, site); chats are merged by time.

## What changes

- AUTH-26 modified: a phone of another account, with the right code, starts a merge instead of `phone_taken`.
- AUTH-30: link an email (email code) or GitHub to the account.
- AUTH-31: a merge needs the proof first; then Dalan says the way has an account and asks to confirm.
- AUTH-32: which account is kept: the one with products; both with products is refused.
- AUTH-33: the kept account takes only the empty fields; two different values are refused.
- AUTH-34: after the merge the other account is gone; sessions, audit log, access log.
- REL-55: chats with the same bot become one chat, by time; other chats, files and push subscriptions move.
- REL-56: each bot that had a chat with the gone account gets one `chat_moved` update; a send to the old `chat_id` gets `chat_moved` with the new id.
- TG-26: the Telegram door shows it as Telegram's `migrate_to_chat_id`.
- CLI-70: account screen: link email and GitHub; the merge question.

## Not in this change

- A verified phone makes a builder at once (auto-builder).
- Unlinking a login way, or splitting an account.
