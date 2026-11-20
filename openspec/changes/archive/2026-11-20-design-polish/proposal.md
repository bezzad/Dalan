# Proposal

## Why

The owner (2026-11-20, with a screenshot of "My bots"): the screens do not follow the design system yet. Button words are not in the middle, buttons in one row are not on one line, the logo and "Dalan" are not on one line, the language link sits among the main links, and the font is not good. He asked for a round globe language button like the theme button, a cleaner logo, a round favicon, and a "closed gate" not-found page made from the logo.

## What Changes

- Buttons: one height per size, words in the middle on both axes; buttons in a row of a card line up.
- Header on every page ends with the same tools: a round language button (globe and EN or FA), the theme button, then "Log in" or the user menu. The language links leave the menus.
- Font: Inter (OFL, served by us) for Latin letters; Vazirmatn stays for Persian.
- Logo: a clean gate drawn in one SVG; the favicon is a round version.
- CLI-40: an unknown address shows a "not found" page with the gate drawn closed.

## Capabilities

### New Capabilities

- `client`: CLI-40.

### Modified Capabilities

- `client`: CLI-35.

## Impact

Client only. One new npm package (`@fontsource-variable/inter`, OFL font files). No new server setting.
