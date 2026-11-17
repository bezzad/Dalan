# Proposal

## Why

After milestone 3 a person can use Dalan only with the development login. Milestone 4 (docs/DESIGN.md section 19) adds the real login, so a real person with an Iranian mobile number can enter, and stays logged in.

## What Changes

- **SMS login**: phone number, a small proof of work in the browser, a code by SMS, then the session. A new number becomes a new user after it gives a name.
- **SMS protection**: 1 SMS per minute and 5 per day for each number, a high daily cap for each address, and a daily budget for the whole server.
- **Password**: the user can set a username and a password (Argon2id). Later logins use phone or username with the password, and need no SMS.
- **Refresh token**: a second cookie that renews the session. It is replaced on every use; reuse of an old one ends that device's session.
- **SMS sending** lives in Notification behind one small contract, `ISmsSender`. Two senders: `log` (Development only) and `kavenegar`.
- **Security log**: logins, failed logins, SMS sends and refresh reuse are logged, never with a code or password.
- **Client**: new login screens (SMS, password), an account screen (username, password), silent session renewal.

Out of scope: TOTP and "SMS code on every login" (two-step login), email as a second login, the audit table and the admin screens (milestone 8), suspend and block of a user (milestone 8), "Login with Dalan" (Layer 2).

## Capabilities

### New Capabilities

- `notification`: how the server sends an SMS.

### Modified Capabilities

- `identity`: SMS login, password, username, refresh token, logout of a device.
- `security`: proof of work, SMS limits, password try limit.
- `client`: proof of work in the browser, session renewal, Persian digits.

## Impact

- New runtime package: `Konscious.Security.Cryptography.Argon2` (Argon2id; .NET has none built in).
- New database migration in `identity`: column `users.password_hash`, tables `sessions` and `sms_codes`.
- `Dalan.Identity` now references `Dalan.Notification.Contracts`.
- New secrets on the server: `Notification__Kavenegar__ApiKey`. `Identity__SigningKey` is now a must for real users.
- The owner needs a Kavenegar account with an approved OTP template before real users can log in.
