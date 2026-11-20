# Design

## Parts

```
visitor ──https──> Caddy ──> Dalan.Host ── host is <name>.<Sites:Domain>? ──> Sites: SiteProxy
                                                                                 │ request into the session
agent (home) ──https/wss──> tunnel.dalanet.ir ──> /api/tunnel/... ──> Sites: TunnelHub
                                                                                 │
agent ──http──> localhost:<port> (the builder's app, unchanged)
```

- **Sites** is a new service: own project, schema `sites`, `DbContext`. It references only `Dalan.Identity.Contracts` (access, key scheme name) and `Dalan.Admin.Contracts` (access log). No other service needs it yet, so it has no `.Contracts` project (YAGNI).
- **TunnelHub** (in memory, one per process): the open session of each site name, and the requests that wait for an answer. With more than one server this becomes shared, like the bot queue.
- **The agent** (`agent/Dalan.Agent`) shares no code with the server, so it can be published alone later (DESIGN section 18). The end-to-end tests run the agent's classes against the test server, so the two sides cannot drift.

## Who may open a tunnel

The same people who may make bots: approved builders and admins (the law wants the owner of a site known by phone, DESIGN section 9). The agent logs in with an access key (AUTH-21), made in "My apps" with one button. A login by a short browser code comes later.

A site name is a DNS label: 3 to `Sites:MaxNameLength` letters, digits and `-`, not starting or ending with `-`, stored in lower case. The first builder who opens a tunnel with a free name owns it. Names in `Sites:ReservedNames` are refused.

## The tunnel protocol

All JSON is snake_case. A body is base64 inside the JSON (simple; the size cap keeps it small).

1. `POST /api/tunnel/sessions` `{"sites":["levelwise","shop"]}` → `{"session":"<random>","sites":[{"name":"levelwise","url":"https://levelwise.<domain>"}]}`. Every name must be free or the caller's; else the whole call fails (`name_taken`, `bad_name`, `site_limit`). The new session replaces any older session of those names; an older WebSocket is closed.
2. Then **one** of:
   - `GET /api/tunnel/sessions/{session}/ws` (WebSocket). The server sends each request as a text message; the agent sends each answer as a text message.
   - `GET /api/tunnel/sessions/{session}/requests?timeout=<seconds>` → `{"requests":[...]}` (empty after the timeout), and `POST /api/tunnel/sessions/{session}/responses` with one answer → 204.
3. A request: `{"id":7,"site":"levelwise","method":"GET","path":"/a?b=1","headers":{"accept":["text/html"]},"body":null}`. An answer: `{"id":7,"status":200,"headers":{"content-type":["text/html"]},"body":"PGgxPi4uLg=="}`.
4. An unknown or ended session answers 404 `session_ended`; the agent then opens a new session.

The agent tries WebSocket first. When it cannot connect (a proxy, a filter), it uses long polling for that connection. After any loss it waits (1, 2, 4 … 30 seconds) and starts again with WebSocket.

A session is **alive** while its WebSocket is open, while a poll waits, or for `Sites:SessionIdleSeconds` after the last poll. A dead session is found when a request comes; there is no sweeper.

## Forwarding

- The server drops hop-by-hop headers both ways (`Connection`, `Keep-Alive`, `Transfer-Encoding`, `Upgrade`, `TE`, `Trailer`, `Proxy-*`) and the visitor's `X-Forwarded-*`, then adds `X-Forwarded-For` (the real address, SEC-10), `X-Forwarded-Proto` and `X-Forwarded-Host`.
- The agent sends the request to `http://localhost:<port>` with `Host: localhost:<port>`, because local development servers often refuse other hosts. The site's name is in `X-Forwarded-Host`.
- A site answer never carries Dalan's own security headers; the builder's app sets its own.
- The site domain must be another registered domain than Dalan's: the server refuses to start when `Sites:Domain` is the host of `Admin:SiteUrl` or under it.

## Access log

Every accepted tunnel call writes `IAccessLog` for the builder (actor `User`), like the other doors. Visitors of a site are anonymous: no row (decision 51), the per-address limit holds.

## Settings (new)

| Setting | Start | Note |
|---|---|---|
| `Sites:Domain` | empty | empty = sites off; for example `dalancloud.ir`; `localhost` in local work (`name.localhost:5000`) |
| `Sites:Scheme` | `https` | for the printed address; `http` in local work |
| `Sites:TunnelUrl` | `https://tunnel.dalanet.ir` | the agent's server, shown in the guide |
| `Sites:MaxSitesPerBuilder` | 20 | like bots |
| `Sites:MaxNameLength` | 32 | |
| `Sites:ReservedNames` | `www,api,tunnel,admin,mail,app,dalan,static,cdn,ns1,ns2` | |
| `Sites:RequestTimeoutSeconds` | 30 | DESIGN section 18 |
| `Sites:MaxBodyMb` | 20 | request and answer, each; like a file |
| `Sites:MaxPendingPerAddress` | 50 | DESIGN section 18 said 20 (assumed); one page load asks for dozens of files at once |
| `Sites:SessionIdleSeconds` | 60 | |
| `Sites:PollTimeoutSeconds` | 30 | maximum wait of one poll |
| `Sites:AccessCheckSeconds` | 60 | a suspend or a lost builder approval ends an open WebSocket within this time |
| `Sites:WebSocketPingSeconds` | 20 | keeps the agent's WebSocket open through proxies; after restart |
| `Sites:AgentFolder` | `agent` | the agent files, next to the server file; the deploy fills it; not in the admin panel |

The agent's server address: `https://tunnel.dalanet.ir` (portability rule: a hostname, never an address), changed with `--server` or `DALAN_SERVER`.

## Delivery (second PR)

- CI builds the agent on each system (Native AOT does not cross systems): linux-x64 on every pull request; win-x64 only before a deploy, because Windows machines cost more CI minutes in a private repository. No macOS yet: macOS machines stayed queued for this private repository on 2026-11-20, and a deploy must never wait on them; `install.sh` already knows the macOS file names and says when the server has none. No linux-arm64 yet (no free runner).
- Dalan serves the files and `install.sh` / `install.ps1` itself: users in Iran may have no GitHub access.
- Caddy: the tunnel host is a normal site block; every other HTTPS host gets a certificate on its first visit after `GET /api/sites/tls` says yes. No wildcard certificate: Let's Encrypt needs a DNS challenge for that, and Caddy has no Arvan DNS module. Limit: Let's Encrypt gives 50 new certificates per registered domain per week, so at most about 50 new site names a week; a wildcard certificate is the later fix.
- A site may set a cookie for the whole sites domain, which other sites then get. Adding the sites domain to the Public Suffix List stops browsers from this; the owner asks for it after buying the domain.
