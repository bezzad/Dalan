# Dalan — Testing

Dalan carries other people's messages, so tests must be many and exact. The specs say what must be true; the tests prove it.

## 1. Rule IDs

- Every requirement in `openspec/specs/**/spec.md` starts with an ID: `AREA-NN`, for example `OPS-01` or `Q-04`.
- Every scenario of that requirement is at least one test.
- A .NET test names its rule with a trait: `[Trait("Rule", "Q-04")]`. A shell test has a line `# Rule: DEL-04`. A client test has `// Rule: CLI-04` and the ID in the test name.
- `scripts/check-rules.sh` fails CI when an ID has no test (rule `DEL-02`).

Areas: `OPS` operations, `ARC` architecture, `DEL` delivery, `Q` bot queue, `REL` relay, `AUTH` identity, `TG` Telegram adapter, `NTF` notification, `ADM` admin, `SEC` security, `LIVE` live updates, `CLI` client, `SITE` sites, `AGT` agent.

## 2. Workflow (test first)

1. Write or change the rule in an OpenSpec change (`openspec/changes/<milestone>/specs`). The owner reviews it.
2. Write the test for each scenario. Run it; it must fail.
3. Write the smallest code that makes it pass.
4. When the milestone is done, archive the change so `openspec/specs` holds the current truth.

## 3. Test kinds

| Kind | Tool | What |
|---|---|---|
| Unit | xUnit v3 | Pure rules: queue expiry, "latest command wins", token hashing, limits |
| Fakes | `tests/Shared/Fakes.cs` | The SMS provider is a fake that keeps what was "sent"; the log is collected so a test can read it. The Kavenegar sender is tested against a fake HTTP handler (in `Dalan.Host.Tests`) |
| Integration | xUnit v3 + `WebApplicationFactory` + real PostgreSQL (Testcontainers) | HTTP endpoints, database, outbox. Never an in-memory database fake; `jsonb` and locking must be real. Each test gets a new database (`tests/Shared`). Without Docker, set `DALAN_TEST_POSTGRES` to the connection string of a server where the tests may create databases |
| Contract | xUnit v3 + JSON files + the `Telegram.Bot` package | Telegram adapter answers compared with saved examples of the pinned Bot API version (`tests/Dalan.Telegram.Tests/Examples`); one test drives the real `Telegram.Bot` client against the test server |
| Architecture | xUnit v3 + reflection on assemblies | Dependency rules `ARC-*` |
| Client | Node test runner (`node --test`), TypeScript run directly, no test package | Pure client logic: message list, sync, reply buttons, reconnect wait, languages; a scan of the built files for foreign hosts. No browser test in the gate yet: the screens are checked by hand in a real browser |
| Shell | bash | Deploy, server setup, backup and drill scripts with fake system commands and a fake server folder |
| Load | chosen when the first load test is written | Target in `docs/ARCHITECTURE.md` section 8; run before each release to the Iran server, not on every push |

## 4. Rules for writing tests

- One behavior per test. Name: `RuleId_what_happens`, for example `Q04_latest_command_replaces_older_from_same_user`.
- Time is injected (`TimeProvider`), never `Thread.Sleep` or real waiting for expiry.
- No test talks to a foreign host.
- A flaky test is a bug: fix it or delete it, never retry it in the gate.

## 5. Commands

The gate is `scripts/ci.sh` on the owner's computer (rule `DEL-01`): it runs every step of `.github/workflows/ci.yml`, prints PASS or FAIL for each, and stops at the first failure. GitHub CI runs only by hand. `tests/delivery/ci_workflow_test.sh` fails when the two differ, so a new test script or test project goes into both.

Tools it needs (it fails by name when one is missing): the .NET SDK of `global.json`, Node of the major version in `ci.yml` with npm, `clang` and `zlib1g-dev` (`sudo apt install clang zlib1g-dev`), `caddy` (the version `CADDY_VERSION` in `ci.yml`), and Docker or `DALAN_TEST_POSTGRES`. Without Docker only the image test is skipped.

When Docker cannot stop the test containers (seen on the owner's NUC: "Container failed to exit within 10s of kill", then every test project fails in its cleanup), use one long-running PostgreSQL instead. With `DALAN_TEST_POSTGRES` set, `ci.sh` runs the test projects one at a time:

```bash
docker compose -p dalan up -d
DALAN_TEST_POSTGRES='Host=127.0.0.1;Port=5432;Username=dalan;Password=dalan;Database=postgres' bash scripts/ci.sh
```

Each test makes a database `dalan_test_<id>` there and leaves it. Drop them now and then: `docker exec dalan-postgres-1 psql -U dalan -d postgres -Atc "select format('drop database %I with (force);', datname) from pg_database where datname like 'dalan_test_%'" | docker exec -i dalan-postgres-1 psql -U dalan -d postgres -q`.

```bash
bash scripts/ci.sh               # everything, before each merge
(cd client && npm ci && npm run build && npm test)  # client; the server build needs client/dist
dotnet test                      # all .NET tests (needs Docker, or DALAN_TEST_POSTGRES)
bash tests/deploy/deploy_test.sh # deploy script tests; also setup_test, backup_test, drill_test, pull_deploy_test
bash scripts/check-rules.sh      # every rule has a test
openspec validate --all --strict # specs are well formed
```
