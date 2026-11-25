# Tasks

Needs m26 (frame and pieces) and m25. Each task: write the failing tests tagged with the rule IDs, then the code. CI runs on GitHub (rule 11).

- [ ] 0. Owner: `dalanet.ir/dashboard` (recommended) or `dashboard.dalanet.ir` (design.md, decision 1); pick any of the security ideas for a later change
- [ ] 1. Admin: `settings.json` descriptions for every key (both languages, unit, range, group, risky); `/api/admin/settings` gives them; `out_of_range`; test for keys with no description (ADM-19)
- [ ] 2. Admin: `/api/admin/overview` counts through the Identity, Relay and Sites contracts (ADM-19, CLI-82)
- [ ] 3. Security tests: every `/api/admin` call refused for a common user and logged; no admin data in common answers (SEC-14)
- [ ] 4. Client: dashboard frame, grouped menu by role, routes, "Not found" for admin pages, redirects from old addresses; host serves `/dashboard/*` (CLI-81)
- [ ] 5. Client: admin pages moved into the frame; overview cards (CLI-82)
- [ ] 6. Client: settings page with groups, rows, badges, search, risky question (CLI-83)
- [ ] 7. `docs/UI.md`: dashboard frame, overview cards, settings rows; DESIGN and ARCHITECTURE addresses
- [ ] 8. CI green on GitHub, final review, merge; check as admin and as a common user, phone and desktop; archive
