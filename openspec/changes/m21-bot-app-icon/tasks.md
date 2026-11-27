# Tasks

Each task: write the failing tests tagged with the rule IDs, then the code. CI runs on GitHub (rule 11).

- [x] 1. Relay test: width and height read from PNG, JPEG and WebP headers (REL-54)
- [x] 2. `BotIcons.cs`: read and keep the size on save; `Bot` columns and migration, which reads the size of the icons saved before (REL-54)
- [x] 3. Relay tests and `BotIconEndpoints.cs`: manifest with only the bot icon and its real size; Dalan's icons when none fits; `/@{username}/icon`; 404 for unknown or suspended (REL-54)
- [x] 4. Host test and `ClientApp.cs`: the `/@username` page links the bot's icon twice (REL-54)
- [x] 5. Client test and `main.ts`, `messages.ts`: the three links on a chat, Dalan's on other screens (CLI-55)
- [x] 6. CI green on GitHub, final review, merge (#94)
- [ ] 7. On dalanet.ir: install @mofidbot in Chrome (Install and Create shortcut) and on a phone; the app shows the bot's icon; archive (after m13)
