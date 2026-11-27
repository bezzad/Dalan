# Design

## Decisions

1. **Follow the screenshots, not the brand.** The layout and the pieces come from ngrok's settings; colours, font, radius and spacing stay Dalan's tokens (`app.css`, `docs/UI.md`). Nothing is loaded from a foreign host (rule 7); icons are Dalan's own inline SVGs (`icon.ts`).
2. **One address per section.** Back, reload and a shared link land on the same section (CLI-51); the side menu marks the current one.
3. **Theme stays two states.** The owner chose light and dark only (CLI-35); "System" is an idea in m27, not here.
4. **No new API.** Every row uses the calls the old cards used.

## Additions to `docs/UI.md` (the design system)

Add a part "Settings (CLI-77, CLI-78)":

- **Settings frame**: the site header on top; under it a side menu (240px, at the start side) and the section (up to 720px). Below 760px no side menu: `/account` is the menu list, and each section page starts with "‹ Settings".
- **Side menu**: group titles as `.label` (CLI-76), items as 36px rows with an 18px icon and the name; the current item has `--brand-soft` background and `--text` colour; hover has a light `--line` background.
- **Breadcrumb**: small grey text, `Settings › <section>`, the parts are links, `›` turns with the page direction.
- **Section head**: the title (`--text-title`) and one grey line under it that says what the section holds.
- **Setting row** (`.setting-row`): two columns, 1 : 1.4; start: the row title (16px, 600) and its help (14px grey); end: the control, then its button under it. Rows are split by a 1px `--line`; 32px between rows. Below 760px one column.
- **Row button**: plain by default; primary only when the value changed (so a page never shows five green buttons at once).
- **Status badge** (`.badge.ok`, `.badge.off`): small round-cornered tag; ok is `--brand-soft` with `--brand` text and a ✓ dot; off is `--line` with `--muted` text.
- **Method card**: a card per login way: icon, name, badge at the start; the action at the end; under them the details (masked phone, email, GitHub name) as small rows on `--bg`.
- **Radio card**: a card with a radio, a small picture and a name; the chosen one has a `--brand` border and `--brand-soft` background. Used for theme and language.
- **Danger card**: a card with a `--danger` border tint, a title, the text, and a plain button with `--danger` text. Every button in it asks first.
- **Empty state**: centred 32px grey icon, a short bold line, one grey line that says how it fills.
- **Grouped menu** (user menu and later menus): a head with the name and a grey second line; items are 40px rows with an 18px icon; a 1px `--line` between groups; Log out last, alone.
