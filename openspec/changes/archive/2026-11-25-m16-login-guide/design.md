# Design

- **Next path** (`client/src/next.ts`, pure): `safeNext` accepts one string that starts with a single `/`, has no `\`, no control character, no `//` start, at most 200 characters. `loginTarget` picks, in order: `?next=`, the path kept in `sessionStorage` (`dalan.next`, set before the GitHub round trip), the current address; from `/` it is `/bots`.
- **Steps** (`client/src/steps.ts`, pure): `stepStates(done)` gives `done`, `current` (the first step not done) or `todo`. `siteDone` and `botDone` turn what the server says (me, keys, sites) into the `done` list. Nothing new on the server: `GET /api/me`, `/api/account/keys`, `/api/sites`.
- **Key modal**: a `<dialog>` made in `client/src/keyModal.ts`. The key is shown once, inside the login command.
- **Browser open** (`agent/Dalan.Agent/BrowserOpener.cs`): `ShouldOpen` is pure (kind, flag, terminal, environment); `Open` starts `xdg-open`, `open` or `cmd /c start` and swallows every error. The tunnel client calls it once, after the first "Online" line.
- **Audit**: the scripts a user runs are served by Dalan with `__DALAN_URL__`. Foreign hosts found: `deploy/install.sh` (the server's own setup: GitHub for Caddy, dot.net, nodejs.org), and GitHub and sms.ir in the settings, which are features. They are listed in the milestone summary.
