# m47: the home page in the look of ngrok's home page

## Why
The owner (2026-11-28) sent ngrok's home page and asked for the first page to look like it: a dark hero, a big serif
title with a coloured second line, a soft colour cloud behind it, one call in the middle, a strip of names under it,
and product cards with coloured icon badges. Its look only, never its brand, logo or words (docs/UI.md).
This changes the owner's earlier "no glow" (2026-11-22) for the hero only: a still, soft cloud behind the title.

## What changes
- CLI-26 (changed): the hero has one call, "Try it (free)", and under it a strip of what Dalan works with
  (languages and frameworks, as plain words). "See the plans" leaves the hero; the header keeps Plans.
  A new part "Try it now" (owner, 2026-11-28, like ngrok's "share localhost"): the one install line of the site agent
  (Linux and macOS, or Windows) in a pill with Copy, and links to the site and bot guides. No other steps.
- CLI-103 (new): the big titles of the home page (the hero, each section title, the closing call) use a display font:
  Source Serif 4 for Latin letters and Noto Naskh Arabic for Persian (both OFL, from npm, served by Dalan, rule 7).
  A soft, still cloud of the five title colours sits behind the hero title; fainter in the light theme.
  The hero button is a light pill; in English its words are monospace capitals, like the header links.
- CLI-104 (new): each plan and each "What you get" card has its own colour: the badge, the small label, and on a plan
  its traffic picture. On hover a plan card takes a soft tint of its colour and its button turns light.
- CLI-105 (new): the strip moves slowly to the end side, with faded edges; with reduced motion it stands still and wraps.
- CLI-106 (new, owner 2026-11-28, like ngrok's "share localhost" page): a live demo in "Try it now": a browser with
  a small site and a terminal that types `dalan http 3000 --name mysite`; each page opened in that browser adds a line
  to the terminal's log. Drawn in HTML, no video. The real agent prints this log in a later change (owner's choice).
- Only the client (CSS, the landing page, two fonts); no server change and no new setting.
