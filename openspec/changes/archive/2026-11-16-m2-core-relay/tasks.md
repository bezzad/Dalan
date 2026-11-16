# Tasks

## 1. Database and start-up

- [x] 1.1 Add packages, `compose.yaml`, connection string setting and the shared PostgreSQL test fixture; verify a test can open a new database
- [x] 1.2 Write tests for AUTH-01, AUTH-02; add the `identity` schema, `IUserDirectory` and its migration; verify they pass
- [x] 1.3 Write tests for REL-01, REL-02; add the `relay` schema (bots, memberships, messages) and its migration; verify they pass
- [x] 1.4 Write tests for OPS-02, OPS-07, OPS-08; add the start-up worker and the 503 gate; verify they pass

## 2. Bots and tokens

- [x] 2.1 Write tests for REL-03, REL-04; add token create, hash and check; verify they pass

## 3. Queue

- [x] 3.1 Write unit tests for Q-02, Q-03, Q-04, Q-08; build the per-bot queue; verify they pass
- [x] 3.2 Write tests for Q-01, REL-05, REL-06, REL-09, REL-10; build `Chats.ReceiveFromUser`; verify they pass
- [x] 3.3 Write tests for Q-07 and the sweep of Q-02; build reload on start and the expiry sweep; verify they pass

## 4. Bot API

- [x] 4.1 Write tests for REL-07, Q-05, Q-06, Q-09; build `GET /v1/updates`; verify they pass
- [x] 4.2 Write tests for REL-08; build `POST /v1/messages`; verify they pass
- [x] 4.3 Write tests for SEC-01, SEC-02; add the rate limits and forwarded headers; verify they pass

## 5. Finish

- [x] 5.1 Update `docs/ARCHITECTURE.md`, `docs/DESIGN.md`, `docs/TESTING.md`, `CLAUDE.md`, `README.md`; verify every command in them runs as written
- [x] 5.2 Verify CI is green, review the whole diff, archive this change

## Workflow follow-up

- The owner delegated the spec review for this milestone ("if Fable approves, I approve too") and said to use the section 18 values as defaults.
- Archived in the same pull request, so `openspec/specs` holds bot-queue, relay, security and identity.
