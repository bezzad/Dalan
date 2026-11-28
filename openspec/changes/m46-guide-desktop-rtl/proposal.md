# m46: the guides on a computer, and Persian as a true mirror

## Why
The owner (2026-11-28): on the site guide in English the steps sit on the left, but in Persian they sit in the middle
of the page, not on the right. The guide is also drawn for a phone only: on a computer it is a narrow column of cards.
The middle column in Persian is on every public page with a narrow column (FAQ, About, Contact, Showcase, the guides):
the scroller is left to right for its scrollbar, so a column narrower than the page stayed on the left side of it.

## What changes
- CLI-101 (new): in Persian a public page's column starts at the right edge, the mirror of English.
- CLI-102 (new): on a computer (960px and wider) a guide uses the page width: the step names are a list at the start side,
  and the open step is one big card beside it, with its own title. One step is always open there.
  A phone and a tablet keep the cards one under another, now as wide as the page.
- Only CSS and the stepper; no server change and no new setting.
