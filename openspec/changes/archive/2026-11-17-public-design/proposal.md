# Proposal

## Why

The landing page is a list of four text cards. A visitor does not see what Dalan does: that the bot stays on a home computer, that the computer only calls out to Dalan, and that users reach the bot through Dalan. The owner asked for public pages that explain this with a picture and clear steps, in the style of good developer tools sites (ngrok was the example: inspired by its clarity, nothing copied).

## What Changes

- **Design system**: colour, space, radius and type tokens in `app.css`; a wide layout for the public pages (header, sections, footer); a code block style. Light and dark, right to left and left to right, as before.
- **Landing page**:
  - A hero: a short title, one sentence, "Enter" and "How it works".
  - A diagram drawn in the page (no image file): user, Dalan in Iran, your computer. The arrow from the computer goes out to Dalan only; "no public IP, no open port".
  - Three numbered steps: get a token, change one line (the server address), run on your computer. The code shows the real addresses from the public settings (rule 4).
  - The three messages of DESIGN section 13, in order, as feature cards with icons.
  - A closing call to action and a footer.
- **Privacy, FAQ and showcase** use the same header, layout and footer.

Out of scope: new server endpoints, Layer 2 (sites), animations beyond one moving dot, images or fonts from any other host (rule 7).

## Capabilities

### Modified Capabilities

- `client`: CLI-26 (landing page) gets the hero, the diagram and the steps.

### New Capabilities

None.

## Impact

Client only (`client/src/pages.ts`, `client/src/public.ts`, `client/src/i18n.ts`, `client/public/app.css`). No new setting, no migration.
