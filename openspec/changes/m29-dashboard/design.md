# Design

## Decisions

1. **`dalanet.ir/dashboard`, not `dashboard.dalanet.ir` (owner confirmed, 2026-11-25).** A path on the same site is safer and simpler:
   - The session cookies are host-only and `SameSite=Strict` (`SessionTokens.cs`). A new host has no session: it needs its own login hand-over, or the cookies would need `Domain=dalanet.ir`, which also sends them to `api.dalanet.ir` and `tunnel.dalanet.ir`, a wider target for no gain.
   - The chats, the installed bot apps (`/@username`, CLI-55) and the service worker live on `dalanet.ir`; a second origin splits the PWA, the push subscription and the local settings (theme, language) in two.
   - A new host needs DNS, a certificate, Caddy and `Dalan:TrustedProxies` work (rule 13), and its own Enamad check.
   If the owner still wants the subdomain, do it as a later change with a one-time sign-in hand-over, never with a shared cookie.
2. **The menu is not the lock.** The client hides what a user may not use; the server refuses it. Admin data never comes in a common user's answers.
3. **Settings descriptions live next to the catalog.** One embedded `settings.json` keyed by the setting key; a key with no description still shows (its key as the name), so a new limit never disappears; a test lists the keys without a description so they get one.
4. **Risky settings ask first.** A setting marked `risky` (for example the address limits, retention days, Bot API address) asks "Are you sure?" with the old and new value before it saves.

5. **Admin second factor on every way in (SEC-15).** AUTH-37 lets SMS, email and GitHub logins skip TOTP for common users; for an admin each of them could be the weak door (a SIM swap, a mailbox or GitHub break-in), so an admin needs the code on all of them. Admins are only the phones in `Identity:AdminPhones` (server environment, hidden from the panel by `SettingsCatalog`), so the owner stays the only admin unless the server's environment changes.

## Security ideas for later (owner picks; not in this change)

- **Fresh login for admin writes**: changing a setting, suspending a bot or approving a builder needs a login (or a TOTP code) from the last 15 minutes.
- **Admin address allow list**: admin calls only from listed addresses or ranges (a setting the panel itself cannot change, like the secrets).
- **Short admin sessions**: an admin's session ends after 30 minutes without use.
- **Alerts**: an SMS or email to every admin when a setting changes or a new admin is added.
- **Content Security Policy** on every page (`script-src 'self'`, no inline scripts): a bug that lets someone inject text cannot run a script with an admin's session.
- **Read-only admin role**: support people can read messages and logs but change nothing.
