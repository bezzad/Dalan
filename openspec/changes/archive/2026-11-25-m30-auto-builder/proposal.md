# Proposal

## Why

Owner decision of 2026-11-25: a verified mobile number is enough to build bots and sites. Waiting for the admin slows every new builder, and the phone already ties the account to a real person (rule 13).

## What changes

- AUTH-19 modified: with `Identity:AutoBuilder` on, "become a builder" approves at once. An admin's refusal is kept as `refused`; a refused user may ask again, and that request waits for the admin.
- AUTH-39: with `Identity:AutoBuilder` on, a user with a verified phone and builder state `none` becomes `approved` at an SMS login, when a phone is added (AUTH-26) and after a merge (AUTH-33). The requests open at the change are approved once.
- CLI-23 modified, CLI-84: the account screen and the admin screen show `refused`; the admin can approve a refused user.

## Not in this change

- Limits per builder stay as they are (`Relay:MaxBotsPerBuilder`, `Sites:MaxSitesPerBuilder`).
