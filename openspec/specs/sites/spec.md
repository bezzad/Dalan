# sites Specification

## Purpose
Defines how a builder's local web app gets a public address under the sites domain: site names, tunnel sessions from the agent, and routing of public requests through the tunnel, kept apart from the Dalan site itself.

## Requirements

### Requirement: SITE-01 Sites own their schema
The Sites service SHALL keep sites in the PostgreSQL schema `sites`, one row per site with its owner's user id and its name, the name unique.

#### Scenario: Schema after start
- **WHEN** the server has started on an empty database
- **THEN** the table `sites.sites` exists with a unique name

### Requirement: SITE-02 Site names
A site name SHALL be 3 to `Sites:MaxNameLength` characters of `a-z`, `0-9` and `-`, not starting or ending with `-`, compared and stored in lower case. A name in `Sites:ReservedNames` SHALL be refused.

#### Scenario: Good name in capitals
- **WHEN** a builder opens a tunnel for `MySite`
- **THEN** the site `mysite` is made

#### Scenario: Bad or reserved name
- **WHEN** a builder opens a tunnel for `-x`, `a_b` or `api`
- **THEN** the call fails with `bad_name` and no site is made

### Requirement: SITE-03 Only builders open tunnels; the first claim owns the name
Opening a tunnel SHALL need a session or an access key of an approved builder or an admin. A free name SHALL become the caller's site. A name owned by another user SHALL fail with `name_taken`, and a new site over `Sites:MaxSitesPerBuilder` with `site_limit`. When one name fails, no site of the call is made.

#### Scenario: Not a builder
- **WHEN** a user who is not a builder opens a tunnel
- **THEN** the call fails with 403 `not_builder`

#### Scenario: Name of another builder
- **GIVEN** builder A owns `mysite`
- **WHEN** builder B opens a tunnel for `mysite`
- **THEN** the call fails with 409 `name_taken`

#### Scenario: Too many sites
- **GIVEN** a builder who owns `Sites:MaxSitesPerBuilder` sites
- **WHEN** they open a tunnel for a new name
- **THEN** the call fails with `site_limit`

### Requirement: SITE-04 One session per site, the newest wins
Opening a tunnel SHALL return a session id and the public address `<scheme>://<name>.<Sites:Domain>` of each site. A newer session of a site SHALL take that site from the older session. A session left with no site SHALL end, and its WebSocket SHALL be closed. A call on an unknown or ended session SHALL fail with 404 `session_ended`. With `Sites:Domain` empty, opening a tunnel SHALL fail with 503 `sites_off`.

#### Scenario: Second agent for the same site
- **GIVEN** an agent with an open session for `mysite` only
- **WHEN** a second agent opens a session for `mysite`
- **THEN** visitor requests go to the second agent, and the first agent's calls get `session_ended`

#### Scenario: Sites off
- **GIVEN** `Sites:Domain` is empty
- **WHEN** a builder opens a tunnel
- **THEN** the call fails with 503 `sites_off`

### Requirement: SITE-05 WebSocket transport
`GET /api/tunnel/sessions/{session}/ws` SHALL accept a WebSocket from the session's owner. The server SHALL send each visitor request as one text message and take each answer as one text message.

#### Scenario: Request over WebSocket
- **GIVEN** an agent connected by WebSocket for `mysite`
- **WHEN** a visitor asks `mysite` for `/hello?x=1`
- **THEN** the agent gets the method, path, query, headers and body, and the visitor gets the agent's status, headers and body

### Requirement: SITE-06 Long-polling transport
`GET /api/tunnel/sessions/{session}/requests` SHALL return the waiting requests at once, or wait up to `timeout` seconds (at most `Sites:PollTimeoutSeconds`) and return an empty list. `POST /api/tunnel/sessions/{session}/responses` SHALL take one answer. An answer to a request that already ended SHALL be dropped quietly.

#### Scenario: Request by long polling
- **GIVEN** an agent that polls for `mysite`
- **WHEN** a visitor asks `mysite` for `/`
- **THEN** the poll returns the request, and the answer the agent posts reaches the visitor

#### Scenario: Nothing to do
- **WHEN** an agent polls with timeout 5 and no visitor comes
- **THEN** the poll returns an empty list after 5 seconds

### Requirement: SITE-07 A silent session is offline
A session SHALL count as alive while its WebSocket is open or a poll waits, and for `Sites:SessionIdleSeconds` after its last poll. After that its sites SHALL be offline.

#### Scenario: Agent stops polling
- **GIVEN** an agent that polled once for `mysite`
- **WHEN** `Sites:SessionIdleSeconds` pass with no poll and a visitor comes
- **THEN** the visitor gets the offline answer

### Requirement: SITE-08 Public routing
A request to host `<name>.<Sites:Domain>` SHALL go to that site's session and nowhere else in Dalan. The match ignores a port; `Sites:Domain` MAY carry one for local work (`localhost:5000`). Hop-by-hop headers SHALL be dropped both ways; the visitor's `X-Forwarded-*` SHALL be replaced by the real client address, scheme and host. The answer SHALL NOT get Dalan's security headers. Status 1xx, 204, 304, or a `HEAD` request: no body. With `Sites:Domain` empty, no host is a site.

#### Scenario: Visitor headers
- **WHEN** a visitor from `5.6.7.8` sends `X-Forwarded-For: 1.1.1.1` and `Connection: x-secret` to a site
- **THEN** the agent gets `X-Forwarded-For: 5.6.7.8` and no `Connection` header

#### Scenario: Site paths are the site's
- **WHEN** a visitor asks a site for `/api/me`
- **THEN** the request goes to the agent, not to Dalan's own `/api/me`

#### Scenario: No Dalan headers on a site answer
- **WHEN** a site answers a visitor
- **THEN** the answer has no `Content-Security-Policy` that Dalan added

#### Scenario: Not modified
- **WHEN** the app answers 304 or 204 (even with a body)
- **THEN** the visitor gets that status with no body, and the next request on the same connection works

#### Scenario: Domain with a port
- **GIVEN** `Sites:Domain` `localhost:5000`
- **WHEN** a visitor opens `mysite.localhost:5000`
- **THEN** the request goes to the site `mysite`, and its address is `http://mysite.localhost:5000`

### Requirement: SITE-09 Offline and unknown sites
A request to a site with no alive session SHALL get 502 with a short page that says the site's computer is off (`site_offline`). A request to a name that is not a site SHALL get 404 (`site_not_found`).

#### Scenario: Agent not running
- **GIVEN** the site `mysite` with no agent
- **WHEN** a visitor opens it
- **THEN** the answer is 502 and the page names `site_offline`

#### Scenario: Unknown name
- **WHEN** a visitor opens `nothing.<Sites:Domain>`
- **THEN** the answer is 404

### Requirement: SITE-10 Request timeout
A request with no first answer message after `Sites:RequestTimeoutSeconds` SHALL get 504 (`site_timeout`). A streamed answer that has started SHALL NOT be cut by this time.

#### Scenario: Slow home app
- **WHEN** the agent takes a request and does not answer in time
- **THEN** the visitor gets 504

#### Scenario: Long stream
- **GIVEN** an answer whose first part came in time
- **WHEN** its parts keep coming for longer than the timeout
- **THEN** the visitor gets every part

### Requirement: SITE-11 Size limits
A visitor request body over `Sites:MaxBodyMb` SHALL get 413 and never reach the agent. One answer message over `Sites:MaxBodyMb` SHALL be refused and the visitor SHALL get 502. A streamed answer over `Sites:MaxAnswerMb` in total SHALL be stopped.

#### Scenario: Big upload
- **WHEN** a visitor posts a body over the limit
- **THEN** the answer is 413 and the agent gets nothing

#### Scenario: Endless answer
- **WHEN** a streamed answer passes `Sites:MaxAnswerMb`
- **THEN** the visitor's connection is closed and the agent is told to stop

### Requirement: SITE-12 Waiting requests per visitor address
At most `Sites:MaxPendingPerAddress` (start 200) requests from one client address SHALL be at the agent at once. More SHALL wait in a line, in order, up to `Sites:MaxQueuedPerAddress` (start 1000) requests, each for at most `Sites:RequestTimeoutSeconds`. A request beyond the line, or one that waited too long, SHALL get 429.

#### Scenario: A burst waits
- **GIVEN** `Sites:MaxPendingPerAddress` requests from one address at the agent
- **WHEN** that address sends one more
- **THEN** it waits, and goes to the agent when one of the others ends; another address is served at once

#### Scenario: Too many at once
- **GIVEN** `Sites:MaxPendingPerAddress` requests from one address at the agent and its line full
- **WHEN** that address sends one more
- **THEN** it gets 429 while another address is still served

### Requirement: SITE-13 The sites domain is not Dalan's domain
The server SHALL refuse to start when `Sites:Domain` is the host of `Admin:SiteUrl` or a name under it, because a site's script could then set cookies for Dalan.

#### Scenario: Sites under dalanet.ir
- **WHEN** the server starts with `Admin:SiteUrl` `https://dalanet.ir` and `Sites:Domain` `sites.dalanet.ir`
- **THEN** it stops with an error that names both settings

### Requirement: SITE-14 Access log of the agent
Every accepted tunnel call SHALL record the builder in the access log (ADM-01) with the real address and port.

#### Scenario: Agent opens a session
- **WHEN** an agent opens a session from `9.8.7.6:4000`
- **THEN** the access log has a `User` row for its builder with that address and port

### Requirement: SITE-15 Suspend ends the tunnel
A user who is suspended or no longer a builder SHALL lose their tunnels: new calls fail at once, and an open WebSocket SHALL close within `Sites:AccessCheckSeconds`.

#### Scenario: Suspended while connected
- **GIVEN** an agent connected by WebSocket
- **WHEN** the admin suspends its user and `Sites:AccessCheckSeconds` pass
- **THEN** the WebSocket is closed and the site is offline

### Requirement: SITE-16 My sites
`GET /api/sites` SHALL list the caller's sites with name, address and online state, with the sites domain and the agent's server address for the guide. `DELETE /api/sites/{id}` SHALL delete a site of the caller (or any site, for an admin), end its session and free its name.

#### Scenario: List and delete
- **GIVEN** a builder with the site `mysite` and its agent connected
- **WHEN** they list their sites, then delete `mysite`
- **THEN** the list shows it online, and after the delete the list is empty and a visitor gets 404

#### Scenario: Not your site
- **WHEN** builder B deletes a site of builder A
- **THEN** the answer is 404 and the site stays

### Requirement: SITE-17 Certificate only for a real site
`GET /api/sites/tls?domain=<host>` SHALL answer 200 only when the host is `name.<Sites:Domain>` and the name is claimed, or the host is `Sites:DemoHost` and demos are on, and only to a caller on the server itself (the front proxy asks before it gets a certificate, OPS-15). Every other call SHALL answer 404.

#### Scenario: Claimed and unknown names
- **GIVEN** the claimed site `mysite`
- **WHEN** the front proxy asks about `mysite.<sites domain>`, then `nobody.<sites domain>`
- **THEN** the answers are 200, then 404

#### Scenario: The demo host
- **WHEN** the front proxy asks about the demo host
- **THEN** the answer is 200 when demos are on, else 404

#### Scenario: From the internet
- **WHEN** a client on the internet asks about `mysite.<sites domain>`
- **THEN** the answer is 404

### Requirement: SITE-18 Demo link
A builder's agent SHALL be able to open a demo: the server SHALL give it a random id of `Sites:DemoIdLength` (start 14) characters `[a-z0-9]` and the address `https://<Sites:DemoHost>/<id>/`, which works for `Sites:DemoHours` (start 24) hours. A builder SHALL have at most `Sites:MaxDemosPerBuilder` (start 3) live demos. An empty `Sites:DemoHost` SHALL turn demos off. The demo row SHALL stay after the end, with its owner.

#### Scenario: New demo
- **WHEN** a builder's agent opens a session with a new demo
- **THEN** it gets an address `https://<demo host>/<14 characters>/` that expires in 24 hours

#### Scenario: Too many demos
- **GIVEN** a builder with 3 live demos
- **WHEN** the agent asks for a fourth
- **THEN** the answer is 409 `demo_limit`

#### Scenario: Reconnect
- **GIVEN** a live demo whose agent lost its connection
- **WHEN** the agent opens a session with the same demo id
- **THEN** the demo works again at the same address; another builder's agent gets 404 for that id

### Requirement: SITE-19 Demo routing by path and cookie
On the demo host, a request for `/<id>/<rest>` of a live demo SHALL go to that demo as `/<rest>` with its query, and the answer SHALL set the cookie `dalan_demo=<id>` (`Path=/`, `Secure`, `HttpOnly`, `SameSite=Lax`) until the demo ends. `/<id>` SHALL redirect to `/<id>/`. A request for any other path with the cookie of a live demo SHALL go to that demo unchanged. Any other request SHALL get the 404 page.

#### Scenario: First visit
- **WHEN** a visitor opens `https://<demo host>/<id>/page?x=1`
- **THEN** the agent gets `/page?x=1`, and the answer sets the `dalan_demo` cookie

#### Scenario: The app's own link
- **GIVEN** a visitor with the `dalan_demo` cookie of a live demo
- **WHEN** the page loads `https://<demo host>/app.js`
- **THEN** the agent of that demo gets `/app.js`

#### Scenario: No demo
- **WHEN** a visitor with no cookie opens `https://<demo host>/app.js`
- **THEN** the answer is the 404 page

### Requirement: SITE-20 Demo end
A demo SHALL end when its time is over or when its owner ends it (`DELETE /api/tunnel/demos/<id>`). After the end its address and its cookie SHALL get 410 with the page "this demo has ended", and its tunnel SHALL stop carrying requests for it.

#### Scenario: Time over
- **GIVEN** a demo opened 24 hours ago
- **WHEN** a visitor opens its address
- **THEN** the answer is 410 with the "ended" page, and the agent gets nothing

#### Scenario: Owner stops it
- **WHEN** the owner ends the demo
- **THEN** its address answers 410 at once

### Requirement: SITE-21 The demo host's root goes to Dalan
On the demo host, a request for `/` with no cookie of a known demo SHALL get a 302 redirect to `Admin:SiteUrl`. With the cookie of a known demo, `/` SHALL follow SITE-19 and SITE-20. Any other path without a demo SHALL keep the 404 page. An empty `Admin:SiteUrl` SHALL keep the 404 page for `/`.

#### Scenario: Bare domain
- **GIVEN** `Admin:SiteUrl` is `https://dalan.test`
- **WHEN** a visitor with no cookie, or with the cookie of no demo, opens `https://<demo host>/`
- **THEN** the answer is 302 to `https://dalan.test/`

#### Scenario: Root of a live demo
- **GIVEN** a visitor with the `dalan_demo` cookie of a live demo
- **WHEN** the page loads `https://<demo host>/`
- **THEN** the agent of that demo gets `/`

#### Scenario: Wrong demo link
- **WHEN** a visitor with no cookie opens `https://<demo host>/nosuchdemo/`
- **THEN** the answer is the 404 page

### Requirement: SITE-22 Streamed answers
The agent SHALL be able to send an answer in parts (`more: true` until the last). The server SHALL write and flush each part to the visitor at once, and SHALL give the agent `Sites:StreamChunkKb` and `Sites:StreamWindow` when it opens a session. At most the window of parts SHALL be on the way per answer; more SHALL end that answer. When the visitor leaves, the agent SHALL be told to stop.

#### Scenario: Server-Sent Events
- **GIVEN** an app that sends one event, waits, then sends another
- **WHEN** a visitor opens the event stream through Dalan
- **THEN** the visitor gets the first event before the second is sent, and over WebSocket each written part gets an `ack`

#### Scenario: Visitor leaves
- **WHEN** a visitor closes a stream that is still running
- **THEN** over WebSocket the agent gets `cancel` for it and stops reading the app

#### Scenario: Stream by long polling
- **GIVEN** an agent on long polling
- **WHEN** it streams an answer
- **THEN** each part's `POST` returns after the part was written, the visitor gets every part in order, and a part for a request that ended gets 410 `request_gone`

### Requirement: SITE-23 A stopped agent is offline at once
`DELETE /api/tunnel/sessions/{session}` SHALL end the caller's session. When the WebSocket of a session closes, the server SHALL end that session. Visitors waiting on an ended session SHALL get 502 `site_offline` at once.

#### Scenario: Ctrl+C
- **GIVEN** a site online
- **WHEN** its agent ends the session
- **THEN** the next visitor gets 502 `site_offline` at once, and the list shows the site offline

#### Scenario: WebSocket closed
- **WHEN** the agent's WebSocket closes
- **THEN** the session ends and a visitor gets 502 at once

### Requirement: SITE-24 Reserve a name
`POST /api/sites` with `{"name": "<name>"}` SHALL claim the name for the calling builder by the rules of SITE-02 and SITE-03, with no agent: 201 with the site (offline), 200 when the caller already owns it, 400 `bad_name`, 409 `name_taken` or 409 `site_limit`.

#### Scenario: Reserve
- **WHEN** a builder reserves `mysite`
- **THEN** the answer is 201, the list has `mysite` offline, and another builder gets 409 `name_taken` for it

#### Scenario: Taken
- **GIVEN** `shop` belongs to another builder
- **WHEN** a builder reserves `shop`
- **THEN** the answer is 409 `name_taken`

### Requirement: SITE-25 The admin hears of each new site
Any builder MAY make a permanent site (owner, 2026-11-27: for now, with no approval). Each new site name, by a first claim (SITE-03) or a reserve (SITE-24), SHALL add a notice (NTF-12) for every admin: the site address, the builder's name and id, and a link to the builder's admin page (CLI-93), so the admin can check it and delete the site or suspend the user. A demo link (SITE-18) SHALL NOT.

#### Scenario: Reserve
- **WHEN** user 7 reserves `shop`
- **THEN** each admin's inbox has a notice with `shop.<sites domain>`, user 7's name and the link `/dashboard/admin/users/7`

#### Scenario: Same name again
- **WHEN** user 7's agent connects again with `shop`
- **THEN** no new notice is added

### Requirement: SITE-26 Login on the proxy
A site owner MAY require Dalan login on the proxy (in My apps, or `dalan http --login`). Then a visitor with no login cookie SHALL be sent through AUTH-41 (Dalan's own client) and back to `/.dalan/callback` on the site host, which sets a host-only cookie (HttpOnly, Secure, `Sites:LoginCookieHours`, start 24). Each request SHALL reach the site with `X-Dalan-User` (AUTH-43 `sub`) and `X-Dalan-Name`; the visitor's own headers of these names SHALL be dropped. `/.dalan/logout` SHALL end it.

#### Scenario: A visitor with no login
- **GIVEN** `shop` requires login
- **WHEN** a visitor opens `https://shop.dalans.ir/cart`
- **THEN** they go to Dalan's login and consent, then back to `/cart`, and the site gets `X-Dalan-User` and `X-Dalan-Name`

#### Scenario: Forged header
- **WHEN** a visitor sends their own `X-Dalan-User`
- **THEN** the site never sees it
