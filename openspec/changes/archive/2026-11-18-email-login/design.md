# Design

## Decisions

1. **Level = has a phone.** No new column: a user with `phone` is level 2. Simple, and it cannot drift.
2. **One code store for both channels.** `SmsCode` and `EmailCode` share a base class `LoginCode` (target, hash, address, time, tries, used); one generic class does the limits and the check. Two tables, so the SMS budget and the email budget are counted apart.
3. **Email is stored in lower case** after a trim, and checked with a simple shape (`x@y.z`, at most 254 characters). No DNS check.
4. **The challenge endpoint takes `phone` or `email`.** The MAC ties the challenge to that one text, as before.
5. **Adding a phone reuses `POST /api/auth/sms`** to send the code. Only the check is new: `POST /api/account/phone`. A phone of another user gets 409 `phone_taken`; a user who has a phone already gets 409 `has_phone`.
6. **A session that started with an email code** may set a new password without the old one, like SMS (it proved the email).
7. **SMTP with `System.Net.Mail.SmtpClient`** (STARTTLS on port 587). No new package. Plain text mail in Persian and English.
8. **Access log:** new actor `Email` and column `email`, like `Phone` (ADM-04).
9. **Spam:** the owner does not mind spam folders (2026-11-18). The client only tells the person to check the spam folder.

## New settings (starting values)

| Key | Start | Panel |
|---|---|---|
| `Identity:EmailCodeDigits` | 6 | yes |
| `Identity:EmailCodeSeconds` | 600 | yes |
| `Identity:EmailCodeTries` | 5 | yes |
| `Identity:EmailWaitSeconds` | 60 | yes |
| `Identity:EmailPerAddressPerDay` | 5 | yes |
| `Identity:EmailPerIpPerDay` | 200 | yes |
| `Identity:EmailPerDay` | 1000 | yes |
| `Notification:EmailProvider` | empty | no |
| `Notification:LogEmailAddresses` | empty | no |
| `Notification:Smtp:Host` | empty | no |
| `Notification:Smtp:Port` | 587 | no |
| `Notification:Smtp:UserName` | empty | no |
| `Notification:Smtp:Password` | empty (secret) | no |
| `Notification:Smtp:From` | empty | no |
| `Notification:Smtp:StartTls` | true | no |
| `Notification:Smtp:TimeoutSeconds` | 10 | yes |
