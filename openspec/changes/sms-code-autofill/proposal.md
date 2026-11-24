# Proposal

## Why

The owner (2026-11-24): every phone should read the login code from the SMS by itself. iPhones already do it: the code field has `autocomplete="one-time-code"`. Android Chrome needs the WebOTP API.

## What Changes

- CLI-59: on the SMS code screen the client asks the browser for the code (WebOTP: `navigator.credentials.get({ otp: { transport: ["sms"] } })`). When the browser gives it, the client fills the field and logs in. A browser without WebOTP, or a user who types the code, works as before. The request ends when the user leaves the screen or sends the form.
- The SMS.ir template ends with the line `@dalanet.ir #CODE#` (the site domain); docs/SERVER.md says so. Both Android and iOS bind the code to that domain.

## Capabilities

### Modified Capabilities

- `client`: CLI-59 (new).

## Impact

Client only: a small webOtp.ts and login.ts. No server change, no new setting. The owner changes the template text in the SMS.ir panel (new approval).
