# Proposal

## Why

The owner (2026-11-18) said the texts of the site are not aligned, and asked for a light theme next to the dark one. The default is the device's theme. ngrok stays the design reference.

## What Changes

- A theme button (system, light, dark) in the top bar of the public pages and of the bot list. The choice is kept on this device. With no choice the site follows the device.
- One set of colour tokens for the whole client: neutral light and neutral dark, one green. The public pages are no longer dark for every visitor.
- Alignment: the top bar items share one box size, so the words sit on one line; checklist items with two lines keep the icon on the first line; a Latin bot name in a Persian list starts at the right.

## Capabilities

### Modified Capabilities

- `client`: CLI-26 (landing page follows the theme), new CLI-35 (theme).

## Impact

Client only. No new server setting (a theme is a visitor's choice, not an admin limit).
