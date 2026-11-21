# Proposal

## Why

The owner (2026-11-21): the language button must look like the theme button, a round button of the same size, with only a globe; the page itself shows the new language, Persian stays the default. And English text must never name Iran: sanctions and search engines (SEO) can hurt the site. Persian text may name Iran.

## What Changes

- CLI-35: the language button is a circle of the same size as the theme button (36px) with only the globe inside. One click switches Persian and English. It sits in the same place in the fixed header on every page.
- CLI-41 (new): no English text the product shows names Iran: client texts, titles, meta, manifest, `/llms.txt`, API error texts. The English "international internet is cut" lines become neutral ("Keeps working when outside links are down").
- CLI-26: the third landing message in English is "Keep working when outside links are down."
- docs/DESIGN.md section 13 and docs/UI.md follow.

## Capabilities

### New Capabilities

- `client`: CLI-41.

### Modified Capabilities

- `client`: CLI-26, CLI-35.

## Impact

Client texts and CSS; the `llms.txt` text; two API error texts in Identity. No new setting, no new package.
