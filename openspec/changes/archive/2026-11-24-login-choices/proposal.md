# Proposal

## Why

The owner (2026-11-24):

1. "Too many codes were sent today… try tomorrow" sends people away for good. A person must always be able to try again, with a rate limit: a timer of 2 minutes (a setting) and a "send again" button.
2. A new person comes for SMS; a returning one is fastest with GitHub or email. Order: new SMS > GitHub > Email; returning GitHub > Email > SMS.
3. SMS and email are buttons like GitHub, not links, so Google and others can be added later the same way.
4. The cap stays (SMS cost, SMS bombing of someone else's number), but not for admin numbers.

## What Changes

- SEC-07: one SMS per number in each `Identity:SmsWaitSeconds` (start 120, was 60). After `Identity:SmsPerPhonePerDay` (5) SMS in 24 hours the wait becomes `Identity:SmsLongWaitSeconds` (new, start 1800) from the last SMS. Both answers are 429 `sms_wait` with `Retry-After`; `sms_limit` for one number is gone. Numbers in `Identity:AdminPhones` never get the long wait. The answer of a sent code says the wait before the next one (`retry_in`).
- SEC-08: over `Identity:SmsPerIpPerDay` the answer stays 429 `sms_limit`, now with `Retry-After` (when the oldest SMS of that address leaves the 24 hours).
- SEC-13: email the same way: `Identity:EmailLongWaitSeconds` (new, start 1800) after `Identity:EmailPerAddressPerDay`; `email_wait`; `email_limit` only for the address limit, with `Retry-After`.
- CLI-36: the login card is a list of equal buttons, one per way: SMS, GitHub, email (each only when it is on). A browser that never logged in (no `dalan.returning` in local storage) gets SMS > GitHub > Email; after any login, GitHub > Email > SMS. SMS and email open their form with a "back" button. Password and the development login stay small links.
- CLI-61 (new): the code screen has "Send again" with a countdown from `retry_in`; at zero it sends a new code. A 429 with `Retry-After` shows "Try again in m:ss", never "tomorrow".

## Capabilities

### Modified Capabilities

- `security`: SEC-07, SEC-08, SEC-13.
- `client`: CLI-36, CLI-61 (new).

## Impact

Identity (LoginCodes, SMS and email endpoints), client login screen and texts. New settings `Identity:SmsLongWaitSeconds` and `Identity:EmailLongWaitSeconds` (admin panel, ADM-06). Changed start value `Identity:SmsWaitSeconds` 60 to 120. No migration.
