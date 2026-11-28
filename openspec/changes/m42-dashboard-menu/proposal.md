# m42: the dashboard menu takes every click

## Why
The owner (2026-11-28): on a computer, after "My bots", the side menu no longer reacts: the pointer does not turn into a
hand and a click does nothing. A workspace page (Chats, My bots, My apps) keeps its own screen inside the dashboard frame,
and that screen's list reaches outward by the page gutter (`main > .scroller`, made for a full-page screen). On a wide
window the gutter is wider than the gap, so the list lies over the side menu and takes its clicks. m40 (toasts) fixed a
different cause on phones.

## What changes
CLI-81: a new scenario: every side menu item takes a click on every dashboard page. The workspace screen has no gutter
inside the frame. A browser test (Chromium, a wide window) checks that each menu item is the top element where it is drawn.
