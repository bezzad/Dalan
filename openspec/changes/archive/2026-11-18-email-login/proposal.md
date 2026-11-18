# Proposal

## Why

An SMS provider (Kavenegar) is not ready yet, and SMS costs money. The owner wants people to start with an email: **level 1** signs in with a one-time code sent by email and can use bots. **Level 2** is a verified Iranian mobile number. Only level 2 can ask to become a builder, because by law the owner of a bot or site must be known by phone (owner, 2026-11-18).

## What Changes

- **Email login (level 1):** `POST /api/auth/email` sends a one-time code (proof of work first, like SMS), `POST /api/auth/email/verify` logs in and makes the user when the address is new. The login screen says "check your spam folder too".
- **Users:** a user has a phone, an email, or both. Phone and email are each unique. `GET /api/me` says `email` and `has_phone` (never the phone).
- **Level 2:** `POST /api/account/phone` adds a phone to a logged-in user with an SMS code (the code is asked with the normal `POST /api/auth/sms`). `POST /api/account/builder` needs a phone: else 403 `phone_required`.
- **Password login** also takes the email address.
- **Email sender:** contract `IEmailSender` in Notification. Providers: `smtp`, `log` (Development, or only listed addresses on a server), or empty.
- **Legal record:** every sent email code gets an access log row with the address and the real client address and port, like SMS. Logged-in email users are recorded like everyone else (no change needed: the log is by user id). Admin can search the access log and users by email.

Out of scope: email for users who already have a phone, changing or removing a phone or email, merging two accounts, HTML email, email notifications.

## Capabilities

### Modified Capabilities

- `identity`: AUTH-01, AUTH-04, AUTH-13, AUTH-14, AUTH-16, AUTH-17, AUTH-19; new AUTH-24 to AUTH-26.
- `security`: SEC-06 (proof of work also before an email); new SEC-13 (email limits).
- `notification`: new NTF-10, NTF-11.
- `admin`: ADM-10, ADM-11; new ADM-16.
- `client`: CLI-23; new CLI-33, CLI-34.

## Impact

- Migrations: `identity.users.phone` nullable, `identity.users.email` (unique), table `identity.email_codes`; `admin.access_log.email`.
- New settings: listed in design.md.
- Docs: DESIGN section 9, ARCHITECTURE sections 7 and 11 and the decisions log, SERVER.md (`dalan.env` lines for SMTP).
