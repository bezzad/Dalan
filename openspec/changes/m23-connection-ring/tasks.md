# Tasks

Each task: write the failing tests tagged with the rule IDs, then the code. CI runs on GitHub (rule 11).

- [ ] 1. Client tests: down and up set and clear the ring on the header icon (bot icon in a chat, logo elsewhere), the tooltip and the status text; `app.css` has no `body.offline::before` (CLI-75)
- [ ] 2. `main.ts`, `chatHeader.ts`, header logo, `i18n.ts`: the ring state instead of `body.offline` and `data-connecting` (CLI-75)
- [ ] 3. `app.css`: the ring (thin, `--brand`, turns), still and dashed with reduced motion; remove the bar and its height rule (CLI-75); `docs/UI.md` header
- [ ] 4. CI green on GitHub, final review, merge
- [ ] 5. On dalanet.ir: open a chat, turn the network off and on; the ring turns and goes, the tooltip changes, nothing moves; archive
