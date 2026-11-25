# Proposal

## Why

The owner shared ngrok's settings screens (2026-11-25) and asked for every feature or idea Dalan could use. These are the ones that fit Dalan; each is its own rule, so the owner can keep or drop each one before work starts. They build on the settings area of m26.

## What Changes

1. **Sessions (AUTH-35, in "Login and security").** A list of the devices where the user is logged in (browser and system from the user agent, address, last used, "this device"), each with "Log out", and "Log out on all other devices". Sessions already exist per device (a password change signs out the others).
2. **Delete my account (AUTH-36, a danger card in Profile).** Needs the password or a fresh code, and a typed confirm word. Refused while the user owns a bot or a site (delete them first). The user's profile, keys and sessions go; the legal records stay for their retention time (rule 13: `admin.access_log`, sent codes). The phone and email can sign up again.
3. **Second factor (AUTH-37, in "Login and security").** Optional TOTP (an authenticator app) for password logins: set up with a QR code drawn on the device and one code; ten one-time recovery codes shown once; turning it off needs a code. SMS, email and GitHub logins already prove the person and do not ask for it.
4. **Address limits on access keys (AUTH-38, in "Access keys").** An optional list of addresses or ranges per key; a call from another address with that key gets 403 `key_address_refused`, and the access log keeps it.
5. **Calendar (CLI-79, in "Preferences").** Dates in the Persian (Jalali) or the Gregorian calendar; Persian starts with Jalali, English with Gregorian. Kept on the device, like the theme.

Not proposed:
- A third "System" theme: the owner chose light and dark only (CLI-35).
- A search box in settings: six sections do not need one.
- A usage badge like "$5 remaining": Dalan has no billing yet; it fits when plans are paid.
- Team members and service users: Dalan accounts are one person; a bot token already acts for a program.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `identity`: new AUTH-35, AUTH-36, AUTH-37, AUTH-38.
- `client`: new CLI-79.

## Impact

- Identity: session list and revoke, account delete, TOTP secret and recovery codes (hashed), key address lists; migrations; audit log entries for each.
- Relay and Sites: refuse delete while the user owns bots or sites (through contracts).
- Client: settings sections of m26; a small QR drawer and TOTP need no package (QR from a short local function, TOTP checked on the server).
- New settings (rule 4): `Identity:TotpStepSeconds` 30, `Identity:TotpWindowSteps` 1, `Identity:RecoveryCodes` 10, `Identity:DeleteConfirmWord` "DELETE" / "حذف", `Identity:MaxKeyAddresses` 20.
