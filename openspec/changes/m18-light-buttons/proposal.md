# Proposal

## Why

GitHub issue #77: in the light theme the buttons on the login page look transparent. A plain button has the same white background as the card (`--surface` #fff), and its border is `--line` (#e2e5e9), about 1.2:1 against white. People cannot see where the button is. WCAG 2.2 (1.4.11) asks 3:1 for the edge of a control.

## What Changes

1. **Plain button border (CLI-71).** A new colour `--control-line` for the border of plain buttons (`button`, `a.button`, the login way buttons). In the light theme it has at least 3:1 contrast against `--surface` and `--bg`. Cards, rows and dividers keep the soft `--line`.
2. Primary, link and coloured (`success`, `danger`, `primary`) buttons do not change.
3. The dark theme does not change.

Out of scope: the border of text fields (same soft line; see design.md, open question), the dark theme.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client`: new CLI-71.

## Impact

- Client: `client/public/app.css` (one new colour, button borders), a new test in `client/tests/style.test.ts`.
- Docs: `docs/UI.md` Buttons section.
- No new settings.
