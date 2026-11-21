# Tasks

## 1. First PR: tunnel, agent, My apps

- [x] 1.1 Sites service: schema, names, tunnel sessions, WebSocket and long polling, public routing (SITE-01 to SITE-16), tests first
- [x] 1.2 Agent: login, http, start, WebSocket then long polling, forward, reconnect (AGT-02 to AGT-09), end-to-end tests against the test server
- [x] 1.3 Architecture rule ARC-06; CI publishes the agent for linux-x64 with Native AOT (AGT-01)
- [x] 1.4 Client: "My apps" with guide (CLI-39); check in a real browser
- [x] 1.5 Docs: ARCHITECTURE (service, decisions 60 and 61), DESIGN section 11, TESTING areas

## 2. Second PR: delivery

- [x] 2.1 CI builds the agent for Linux, Windows and macOS; Dalan serves the files; one-line install
- [x] 2.2 Caddy: the tunnel hostname and a certificate per site name on first visit; setup.sh and docs/SERVER.md
- [ ] 2.3 Run Levelwise through the tunnel; archive this change
- [x] 2.4 The macOS agent (osx-arm64 on `macos-26`, osx-x64 on `macos-26-intel`) in the manual CI run; live on the server
