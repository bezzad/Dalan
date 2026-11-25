# Tasks

Each task: write the failing tests tagged with the rule IDs, then the code. CI runs on GitHub (rule 11). Closes GitHub issue #77.

- [x] 1. Test in `client/tests/style.test.ts`: light `--control-line` has 3:1 against `--surface` and `--bg`; `button, a.button` and `.login .button.way` use it (CLI-71)
- [x] 2. `app.css`: `--control-line` in the light `:root` and both dark blocks; plain button borders use it (CLI-71)
- [x] 3. `docs/UI.md` Buttons: plain buttons have the `--control-line` border
- [x] 4. Owner answer: text fields too? Not answered; kept out of scope (design.md)
- [ ] 5. CI green on GitHub, final review, merge, close #77, archive
