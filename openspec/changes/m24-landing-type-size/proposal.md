# Proposal

## Why

On the landing page the section labels above the titles (for example "امکانات" above "هر آنچه برای ارتباط کاربران با کامپیوتر شما لازم است") are much smaller than the body text (owner, 2026-11-25, screenshot). `.label` is `600 12px` (`app.css`), made for an English uppercase mono label; in Persian it falls to Vazirmatn at 12px, with no capitals to help, so it reads as fine print. The diagram texts are 12px too (`.frame-label`, `ul.guards`, `.branch .box`), and `.soon` is 11px.

## What Changes

1. **One small-text floor on public pages (CLI-76).** No text on a public page (CLI-26 to CLI-29, about, contact, FAQ, privacy) is smaller than 13px in English or 14px in Persian. Code blocks keep their own size.
2. **Section labels** (`.label`: the line above a section title, product labels, footer column titles): 14px in English (uppercase, mono, as now) and 15px in Persian (Vazirmatn, no spacing), so they stand clearly above the body text's fine print but under the titles.
3. **Diagram and tag texts** (`.frame-label`, `ul.guards`, `.branch .box`, `.tag`, `.soon`): 13px in English, 14px in Persian.
4. The sizes live as tokens in `:root` (`--text-label`, `--text-small`), set again under `:lang(fa)`, so later pages use them.

Out of scope: the chat and admin screens (their 13px notes are not titles); the hero and section title sizes, which are right.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client`: new CLI-76.

## Impact

- Client: `client/public/app.css` (tokens and the rules above), a test in `client/tests/style.test.ts`, `docs/UI.md` (type sizes).
- No server change, no new settings.
