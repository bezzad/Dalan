# Dalan

Relay platform hosted in Iran. A bot or web app on a home computer (no public IP) makes only outbound long-polling calls to Dalan; users talk to Dalan; Dalan passes messages between them. Site `dalanet.ir`, Bot API `api.dalanet.ir`.

Read first:
- `docs/DESIGN.md`: what we build and why (product). Source of truth for scope.
- `docs/ARCHITECTURE.md`: services, communication, data, scale, decisions log.
- `docs/TESTING.md`: rule IDs, test-first workflow, test kinds.
- `docs/SERVER.md`: Iran server setup, deploy, backup, shutdown drill.
- `openspec/specs/`: current rules. `openspec/changes/`: work in progress, one change per milestone.

## Commands

```bash
(cd client && npm ci && npm run check && npm run build && npm test)   # client first: the server embeds client/dist
dotnet build Dalan.slnx -c Release          # needs .NET SDK 11 (global.json)
dotnet test -c Release                      # all .NET tests; needs Docker, or DALAN_TEST_POSTGRES=<connection string>
docker compose up -d                        # local PostgreSQL for the next line
dotnet run --project src/Dalan.Host         # run the server; open http://localhost:5000 (development login is on; SMS codes go to the log)
node scripts/demo-bot.mjs 1                 # a small bot to chat with; see the top of the file
node client/public/dalan.mjs                # the builder CLI (needs DALAN_URL and DALAN_KEY); /llms.txt explains it
bash scripts/check-rules.sh                 # every spec rule ID has a test
bash tests/deploy/deploy_test.sh            # deploy script tests (also setup_test, backup_test, drill_test)
bash tests/proxy/caddy_test.sh              # needs caddy on PATH
bash scripts/publish.sh out                 # builds the client, then the self-contained linux-x64 release
node scripts/vapid-keys.mjs                 # a key pair for Web Push (server environment)
openspec validate --all --strict            # OpenSpec CLI 2.1.0
```

## Layout

```
src/Dalan.Host/                    the one process; wires services; /health, /ready; serves the embedded client; real client address; per-address rate limit
src/<Service>/Dalan.<Service>      Identity, Relay (core), Notification, Admin
src/<Service>/Dalan.<Service>.Contracts   the only thing other services may reference
src/Plugins/Dalan.Telegram         Telegram adapter plugin (no data, no domain)
tests/                             one project or script folder per area
client/                            PWA, plain TypeScript, no framework, no bundler; src, public, tests; built into dist
deploy/                            deploy.sh, server setup (install.sh, setup.sh), units, Caddyfile, backup, shutdown drill; docs/SERVER.md
compose.yaml                       PostgreSQL for local work only
openspec/                          specs and changes
```

## Rules

1. KISS, YAGNI, Clean Code. Smallest thing that works. Feature folders inside a service; no Repository or Mediator layers.
2. Services reference only other services' `.Contracts`. Relay never mentions Telegram. Only the host references implementations and plugins. Architecture tests enforce this.
3. Sync calls through contracts; async through the PostgreSQL outbox. One schema per service, no cross-schema joins.
4. Every limit, expiry, size and domain is configuration the admin can change. Never hard-code. Starting values: `docs/DESIGN.md` section 18; use them as defaults without asking (owner, 2026-11-16) and list every new setting in the milestone summary.
5. A bot never receives a user's phone number.
6. Production: one self-contained binary under systemd, deployed by CI over SSH. No Docker on the Iran server (Docker is fine locally).
7. The running product loads nothing from a foreign host: no CDN fonts, scripts or analytics.
8. Security from day one: slow password hashing, hashed bot tokens, rate limits on every public endpoint, proof of work before each SMS, audit log.
9. Spec first: change the rule in an OpenSpec change, owner reviews, write the failing test (tag it with the rule ID), then the code. Archive the change when the milestone is done.
10. Always use the latest stable version of every tool and package; check the real version, do not trust memory.
11. After CI passes on a PR, do a final code review of the whole diff. If all is OK, merge it yourself; if not, fix and repeat. Never merge with red or missing CI.
12. Keep docs short and current.
13. Legal duty: the real IP address, port and time of every user and bot are recorded (`admin.access_log`) to answer cyber police (FATA) complaints. Never read `X-Forwarded-For` yourself: use `HttpContext.Connection` (the host makes it real, only from `Dalan:TrustedProxies`). Every door that knows its caller calls `IAccessLog`; every sent SMS code is recorded with its phone number. A new proxy or load balancer goes into `Dalan:TrustedProxies`. Rules `SEC-10`, `SEC-11`, `ADM-01` to `ADM-04`; `docs/ARCHITECTURE.md` section 11.

## Working with the owner

- Senior .NET developer who mostly reviews. Not a native English speaker: short, simple English.
- Show a short plan before each milestone and wait for OK. Ask one question at a time. Do not guess on anything hard to undo. If you disagree with the design, say so with the reason before building.
