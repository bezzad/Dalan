# Tasks

Needs m26 (frame and pieces) and m25. Each task: write the failing tests tagged with the rule IDs, then the code. CI runs on GitHub (rule 11).

- [x] 0. Owner: `dalanet.ir/dashboard` (2026-11-25); second factor for admins (SEC-15); the owner is the only admin. Other security ideas: later
- [x] 0.1 Identity: second factor (AUTH-37, moved here from m27): set up with QR (SVG from the server), confirm, recovery codes, password login step with a ticket, turn off; settings `Identity:TotpStepSeconds`, `TotpWindowSteps`, `RecoveryCodes`, `TotpTicketMinutes`, `TotpMaxTries`
- [x] 0.2 Client: second-factor card in "Login and security", login code step (CLI-92)
- [x] 1. Admin: `settings.json` descriptions for every key (both languages, unit, range, group, risky); `/api/admin/settings` gives them; `out_of_range`; test for keys with no description (ADM-19)
- [x] 2. Admin: `/api/admin/overview` counts through the Identity, Relay and Sites contracts (ADM-19, CLI-82)
- [x] 3. Security tests: every `/api/admin` call refused for a common user and logged; no API makes an admin; no admin data in common answers (SEC-14)
- [x] 3.1 Identity: admin login needs TOTP on every way; `admin_needs_2fa` until set up; admin cannot turn it off; dashboard shows only the set-up page (SEC-15)
- [x] 4. Client: dashboard frame, grouped menu by role, routes, "Not found" for admin pages, redirects from old addresses; host serves `/dashboard/*` (CLI-81)
- [x] 5. Client: admin pages moved into the frame; overview cards (CLI-82)
- [x] 6. Client: settings page with groups, rows, badges, search, risky question (CLI-83)
- [x] 7. `docs/UI.md`: dashboard frame, overview cards, settings rows; DESIGN and ARCHITECTURE addresses
- [x] 8. CI green on GitHub, final review (and the security review fixes), merge (#102)
- [ ] 9. Owner: check as admin and as a common user, phone and desktop; then archive
