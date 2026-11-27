# Proposal

## Why

The owner ran A real app (an Astro site with a PocketBase backend) through Dalan on 2026-11-24 and found these gaps:

1. **Crash on 204 and 304.** The proxy writes a body for answers that may not have one, so the visitor's connection breaks. Browsers get 304 each time they check a cached file.
2. **A first visit breaks the page.** Mysite's service worker asks for 1018 files at once. Over `Sites:MaxPendingPerAddress` (50) the page's own CSS, JS and fonts get 429. Many mobile users also share one address (CGNAT).
3. **No streaming.** An answer must be complete before it is sent. Server-Sent Events never end, so PocketBase realtime (OAuth sign-in, live support chat) always gets 504.
4. **A stopped agent looks slow, not off.** After Ctrl+C or a dropped connection the session lives until `Sites:SessionIdleSeconds`, so visitors wait 30 seconds and see "did not answer in time".
5. **The agent prints a wrong local link.** `Online: http://mysite.localhost` has no `:5000`.
6. **The name is not reserved.** The "site name" field in My apps only builds the command; another builder can take the name first, and the page never says the name is yours.
7. **Test or service?** `dalan http` runs in the terminal. Closing it, logging out or a restart takes the site offline, and the guide does not say so.

## What Changes

1. **No body where HTTP forbids one (SITE-08).** 1xx, 204, 304 and answers to `HEAD` go out with no body.
2. **A line instead of 429 (SITE-12).** At most `Sites:MaxPendingPerAddress` (now 200) requests of one address go to the agent at once. More wait in a line, up to `Sites:MaxQueuedPerAddress` (1000) for at most `Sites:RequestTimeoutSeconds`. Only a full line or a long wait gets 429.
3. **Streamed answers (SITE-22, SITE-10, SITE-11, AGT-07, AGT-12).** The agent sends an answer in parts as the local app writes it. The visitor gets each part at once. The server tells the agent how many parts may be on the way (`Sites:StreamWindow`), and to stop when the visitor leaves. One answer may be at most `Sites:MaxAnswerMb` (500); one part at most `Sites:StreamChunkKb` (64). This replaces "no streaming" in the first-version limits. WebSocket from the visitor to the app is still out of scope.
4. **Stop means offline (SITE-23, AGT-13).** The agent ends its session on Ctrl+C (`DELETE /api/tunnel/sessions/<id>`). The server also ends a session when its WebSocket closes. Waiting visitors get the offline page at once.
5. **Right local link.** `Sites:Domain` and `Sites:DemoHost` may carry a port for local work (`localhost:5000`). The host match ignores the port.
6. **Reserve a name (SITE-24, CLI-39).** `POST /api/sites {"name"}` claims a name for the caller. My apps has a "Reserve" button next to the name, says at once when the name is taken, and refreshes the list (with online state) while the screen is open.
7. **Run as a service (AGT-14, AGT-15, CLI-39).** `dalan service install` runs the saved sites at start-up (systemd user unit on Linux and launchd on macOS, both restart after a crash; the user's "Run" key on Windows, no admin needed); `dalan service uninstall` removes it. `dalan status` shows the service and each site online or offline. The guide shows "Try it" (`dalan http`, Ctrl+C stops it) and then "Keep it online" (`dalan service install`).

Out of scope: WebSocket from visitors to the app, static upload, custom domains, the signed user header, the inspector, Basic Auth and IP allowlists.

## Capabilities

### Modified Capabilities

- `sites`: SITE-08, SITE-10, SITE-11, SITE-12; new SITE-22, SITE-23, SITE-24.
- `agent`: AGT-07; new AGT-12, AGT-13, AGT-14, AGT-15.
- `client`: CLI-38 (the site guide gets step 5), CLI-39.

## Impact

- Sites: `SiteProxy`, `TunnelHub`, `TunnelSession`, `TunnelEndpoints`, `MySitesEndpoints`, `SitesOptions`.
- Agent: `LocalForwarder`, `TunnelClient`, `Program`, `CommandLine`, new `ServiceInstaller`.
- Client: `apps.ts`, `appGuide.ts`, `guide.ts`, `api.ts`, `app.ts`, `i18n.ts`.
- Docs: `docs/DESIGN.md` section 11 and section 18, `docs/ARCHITECTURE.md` decisions, `llms.txt`.
- New settings: `Sites:MaxQueuedPerAddress` 1000, `Sites:MaxAnswerMb` 500, `Sites:StreamChunkKb` 64, `Sites:StreamWindow` 8. Changed: `Sites:MaxPendingPerAddress` 50 → 200.
