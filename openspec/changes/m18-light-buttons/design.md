# Design

## Cause

`client/public/app.css`, light `:root`: `--surface: #fff`, `--bg: #fafafa`, `--line: #e2e5e9`. The rule `button, a.button` uses `background: var(--surface)` and `border: 1px solid var(--line)`. The login card is also `--surface`, so a plain button on it is white on white with a 1.2:1 edge. `.login .button.way` (SMS, GitHub, email) has the same look.

## Decisions

1. **A new colour, not a darker `--line`.** `--line` also draws cards, rows, dividers and the header line; a darker one would make every page heavy. Only controls get the stronger edge.
2. **Value.** Light `--control-line: #868f9a` (3.28:1 on #fff, 3.14:1 on #fafafa; the old `--line` is 1.26:1). Dark: `--control-line` is the same as the dark `--line`, so the dark theme looks the same.
3. **Which buttons.** `button, a.button` and `.login .button.way`. Not `primary`, `link`, `.style-*`, the header round buttons when they have their own border rule, or buttons with `border: 0` (message menu).
4. **Test.** A pure test reads `app.css`, takes `--control-line`, `--surface` and `--bg` from the light `:root`, and checks the WCAG contrast ratio is at least 3. It also checks that the `button, a.button` rule uses `var(--control-line)`.

## Open question for the owner

Text fields (`input`, `textarea`) also use `--line` on white and are hard to see too. Should they get `--control-line` in the same change?
