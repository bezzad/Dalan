# m43: one scrollbar in a chat

## Why
The owner (2026-11-28), with a screenshot of a chat on a computer: three scrollbars. The page itself scrolled by 1px,
the message list scrolled (as it should), and the empty message box had a scrollbar with arrows.
- The message box: `grow()` set the height to `scrollHeight`, which has no border, but the height is the border box.
  The box was 2px short of its text, so it scrolled at every size.
- The page: the hidden status for screen readers (CLI-75) comes after the full-height screen; absolutely placed there,
  it made the page 1px taller than the window.
Not from m42 (#124): that rule is only inside the dashboard.

## What changes
CLI-86: new scenarios: one to five lines show no scrollbar in the box; in a chat only the message list scrolls, never the
page. A browser test checks both.
