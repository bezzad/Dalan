# m39: toasts that do not cover the menu

## Why
The owner (2026-11-28): in the dashboard, moving between menu items sometimes "freezes" and no item can be tapped.
On a slow network every screen whose call fails shows "Failed. Check the connection…". The error toasts stay 7 seconds
and pile up; on a phone they are as wide as the screen, so a few of them cover the lower menu items and a tap closes a
toast instead of opening the item. The menu itself still works.

## What changes
CLI-97: a toast with the same text as one already shown replaces it (it is not shown twice), and at most 3 toasts are
shown at once; a new one removes the oldest. docs/UI.md, part "Toasts".
