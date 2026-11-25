# Tasks

Each task: write the failing tests tagged with the rule IDs, then the code. CI runs on GitHub (rule 11).

## 1. Design system

- [ ] 1.1 `docs/UI.md`: the "Settings" part from design.md (owner reviews it first)
- [ ] 1.2 `app.css`: side menu, breadcrumb, section head, `.setting-row`, `.badge.ok`/`.off`, method card, radio card, danger card, empty state, grouped menu; light and dark; RTL; 320px
- [ ] 1.3 `icon.ts`: profile, lock, bell, sliders, wrench, key, bots, chats, apps, settings, shield, log-out (inline SVG)

## 2. Settings area

- [ ] 2.1 Client tests: routes and the section for each address; builder-only sections; `/account` on desktop and phone; button turns primary on change (CLI-77)
- [ ] 2.2 `client/src/settings/`: frame and side menu, Profile, Login and security (method cards, merge card), Notifications, Preferences (radio cards), Builder, Access keys (CLI-77)
- [ ] 2.3 `main.ts` routes; host serves `/account/*`; m25's links go to `/account/keys` (CLI-77)

## 3. User menu

- [ ] 3.1 Client test and `main.ts`: head with name and phone or email, three groups with icons (CLI-78)

## 4. Ship

- [ ] 4.1 CI green on GitHub, final review, merge
- [ ] 4.2 On dalanet.ir, Persian and English, light and dark, desktop and 360px phone: every section; archive
