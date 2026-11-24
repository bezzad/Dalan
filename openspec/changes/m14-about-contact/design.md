# Design

## Decisions

1. **Admin owns the messages.** They are admin data, like the audit log; no new service. One table `admin.contact_messages` (id, user id or null, email, text, address, port, at, read).
2. **The email is checked on the server.** A logged-in user who has an email always gets that email on the message, whatever the client sent; others must send a valid email. The client only shows the field locked.
3. **Spam.** No login is needed, so: the per-address limit of the host (SEC-02), plus `Admin:ContactPerAddressPerDay` messages per address in 24 hours (429 `too_many_messages`), plus `Admin:ContactMaxLength` characters. No proof of work: no SMS or email is sent, so a message costs us only a row.
4. **Legal record (rule 13).** The row keeps the real address, port and time (`HttpContext.Connection`). A logged-in sender is also recorded in `admin.access_log` (`IAccessLog`).

## New settings

| Key | Start | Meaning |
|---|---|---|
| `Admin:ContactMaxLength` | 2000 | Longest message text, in characters |
| `Admin:ContactPerAddressPerDay` | 5 | Messages one address may send in 24 hours |
