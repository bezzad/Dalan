# Proposal

## Why

The owner (2026-11-22): the site does not work well on a phone, and the landing page says too much. Make it simple, like ngrok (the principles only, not their brand or words):

- On a phone the header needs a real menu (☰), and every page must line up in portrait and in landscape.
- The home page says only what Dalan does, its features and its plans. No install steps there.
- FAQ and Privacy live only in the footer.
- The install guide shows only after the person picks a plan.

## What Changes

- CLI-42 (new): tick icons in lists sit on the first text line in both languages (the owner saw them off on a phone). The site header links to Plans, Showcase and Guide only. Below 760px they fold into one ☰ button that opens a panel; a link, a click outside or Escape closes it. FAQ and Privacy are only in the footer. No page scrolls sideways on a phone, in portrait or landscape.
- CLI-26: the landing page is hero, plans (the two product cards, each with "Get started" to its guide), what you get, the three messages, security, closing call. The "Problems we solve" menu, the OS command tabs and the three steps with code go away. Simpler look: no rainbow text and no glow.
- CLI-37: the sites plan is built now (CLI-39), so "a local site at a fixed Dalan subdomain" is no longer "soon"; the other items of that group stay "soon".
- CLI-38: `/guide` first asks which plan: a bot (`/guide/bot`, the nine steps) or a site (`/guide/site`, four steps with the one-line agent install).
- CLI-01: the server also serves `/guide/bot` and `/guide/site`.
- docs/UI.md: the header, the mobile menu and the simpler look.

## Capabilities

### New Capabilities

- `client`: CLI-42.

### Modified Capabilities

- `client`: CLI-01, CLI-26, CLI-37, CLI-38.

## Impact

Client pages, texts and CSS; two more page addresses in the host. No new setting, no new package.
