# Proposal

## Why

The owner (2026-11-20): the theme button has three states, and "system" is not needed. The first open already reads the device theme; after that the visitor's choice is kept. The button should switch only light and dark, with a wave animation if it costs no performance. In the same thread: the big footer word should stay at the bottom of short pages.

## What Changes

- Theme button: light and dark only. First open follows the device; a press keeps the choice.
- The new theme grows as a circle from the button (View Transitions API, drawn by the browser on the GPU). No animation without browser support or with reduced motion.
- Public pages: the footer stays at the bottom of the screen on short pages.

## Capabilities

### Modified Capabilities

- `client`: CLI-35.

## Impact

Client only. No new server setting.
