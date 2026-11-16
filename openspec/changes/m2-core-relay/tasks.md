# Tasks

## 1. Database and start-up

- [ ] 1.1 Add packages, `compose.yaml`, connection string setting and the shared PostgreSQL test fixture; verify a test can open a new database
- [ ] 1.2 Write tests for AUTH-01, AUTH-02; add the `identity` schema, `IUserDirectory` and its migration; verify they pass
- [ ] 1.3 Write tests for REL-01, REL-02; add the `relay` schema (bots, memberships, messages) and its migration; verify they pass
- [ ] 1.4 Write tests for OPS-02, OPS-07, OPS-08; add the start-up worker and the 503 gate; verify they pass

## 2. Bots and tokens

- [ ] 2.1 Write tests for REL-03, REL-04; add token create, hash and check; verify they pass

## 3. Queue

- [ ] 3.1 Write unit tests for Q-02, Q-03, Q-04, Q-08; build the per-bot queue; verify they pass
- [ ] 3.2 Write tests for Q-01, REL-05, REL-06, REL-09, REL-10; build `Chats.ReceiveFromUser`; verify they pass
- [ ] 3.3 Write tests for Q-07 and the sweep of Q-02; build reload on start and the expiry sweep; verify they pass

## 4. Bot API

- [ ] 4.1 Write tests for REL-07, Q-05, Q-06, Q-09; build `GET /v1/updates`; verify they pass
- [ ] 4.2 Write tests for REL-08; build `POST /v1/messages`; verify they pass
- [ ] 4.3 Write tests for SEC-01, SEC-02; add the rate limits and forwarded headers; verify they pass

## 5. Finish

- [ ] 5.1 Update `docs/ARCHITECTURE.md`, `docs/DESIGN.md`, `docs/TESTING.md`, `CLAUDE.md`, `README.md`; verify every command in them runs as written
- [ ] 5.2 Verify CI is green, review the whole diff, archive this change
