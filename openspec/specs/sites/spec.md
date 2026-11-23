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
- **WHEN** a builder opens a tunnel for `LevelWise`
- **THEN** the site `levelwise` is made

#### Scenario: Bad or reserved name
- **WHEN** a builder opens a tunnel for `-x`, `a_b` or `api`
- **THEN** the call fails with `bad_name` and no site is made

### Requirement: SITE-03 Only builders open tunnels; the first claim owns the name
Opening a tunnel SHALL need a session or an access key of an approved builder or an admin. A free name SHALL become the caller's site. A name owned by another user SHALL fail with `name_taken`, and a new site over `Sites:MaxSitesPerBuilder` with `site_limit`. When one name fails, no site of the call is made.

#### Scenario: Not a builder
- **WHEN** a user who is not a builder opens a tunnel
- **THEN** the call fails with 403 `not_builder`

#### Scenario: Name of another builder
- **GIVEN** builder A owns `levelwise`
- **WHEN** builder B opens a tunnel for `levelwise`
- **THEN** the call fails with 409 `name_taken`

#### Scenario: Too many sites
- **GIVEN** a builder who owns `Sites:MaxSitesPerBuilder` sites
- **WHEN** they open a tunnel for a new name
- **THEN** the call fails with `site_limit`

### Requirement: SITE-04 One session per site, the newest wins
Opening a tunnel SHALL return a session id and the public address `<scheme>://<name>.<Sites:Domain>` of each site. A newer session of a site SHALL take that site from the older session. A session left with no site SHALL end, and its WebSocket SHALL be closed. A call on an unknown or ended session SHALL fail with 404 `session_ended`. With `Sites:Domain` empty, opening a tunnel SHALL fail with 503 `sites_off`.

#### Scenario: Second agent for the same site
- **GIVEN** an agent with an open session for `levelwise` only
- **WHEN** a second agent opens a session for `levelwise`
- **THEN** visitor requests go to the second agent, and the first agent's calls get `session_ended`

#### Scenario: Sites off
- **GIVEN** `Sites:Domain` is empty
- **WHEN** a builder opens a tunnel
- **THEN** the call fails with 503 `sites_off`

### Requirement: SITE-05 WebSocket transport
`GET /api/tunnel/sessions/{session}/ws` SHALL accept a WebSocket from the session's owner. The server SHALL send each visitor request as one text message and take each answer as one text message.

#### Scenario: Request over WebSocket
- **GIVEN** an agent connected by WebSocket for `levelwise`
- **WHEN** a visitor asks `levelwise` for `/hello?x=1`
- **THEN** the agent gets the method, path, query, headers and body, and the visitor gets the agent's status, headers and body

### Requirement: SITE-06 Long-polling transport
`GET /api/tunnel/sessions/{session}/requests` SHALL return the waiting requests at once, or wait up to `timeout` seconds (at most `Sites:PollTimeoutSeconds`) and return an empty list. `POST /api/tunnel/sessions/{session}/responses` SHALL take one answer. An answer to a request that already ended SHALL be dropped quietly.

#### Scenario: Request by long polling
- **GIVEN** an agent that polls for `levelwise`
- **WHEN** a visitor asks `levelwise` for `/`
- **THEN** the poll returns the request, and the answer the agent posts reaches the visitor

#### Scenario: Nothing to do
- **WHEN** an agent polls with timeout 5 and no visitor comes
- **THEN** the poll returns an empty list after 5 seconds

### Requirement: SITE-07 A silent session is offline
A session SHALL count as alive while its WebSocket is open or a poll waits, and for `Sites:SessionIdleSeconds` after its last poll. After that its sites SHALL be offline.

#### Scenario: Agent stops polling
- **GIVEN** an agent that polled once for `levelwise`
- **WHEN** `Sites:SessionIdleSeconds` pass with no poll and a visitor comes
- **THEN** the visitor gets the offline answer

### Requirement: SITE-08 Public routing
A request whose host is `<name>.<Sites:Domain>` SHALL go to that site's session and nowhere else in Dalan. Hop-by-hop headers SHALL be dropped both ways. The visitor's own `X-Forwarded-*` headers SHALL be replaced by the real client address, the scheme and the host. The site's answer SHALL NOT get Dalan's security headers. With `Sites:Domain` empty, no host is a site.

#### Scenario: Visitor headers
- **WHEN** a visitor from `5.6.7.8` sends `X-Forwarded-For: 1.1.1.1` and `Connection: x-secret` to a site
- **THEN** the agent gets `X-Forwarded-For: 5.6.7.8` and no `Connection` header

#### Scenario: Site paths are the site's
- **WHEN** a visitor asks a site for `/api/me`
- **THEN** the request goes to the agent, not to Dalan's own `/api/me`

#### Scenario: No Dalan headers on a site answer
- **WHEN** a site answers a visitor
- **THEN** the answer has no `Content-Security-Policy` that Dalan added

### Requirement: SITE-09 Offline and unknown sites
A request to a site with no alive session SHALL get 502 with a short page that says the site's computer is off (`site_offline`). A request to a name that is not a site SHALL get 404 (`site_not_found`).

#### Scenario: Agent not running
- **GIVEN** the site `levelwise` with no agent
- **WHEN** a visitor opens it
- **THEN** the answer is 502 and the page names `site_offline`

#### Scenario: Unknown name
- **WHEN** a visitor opens `nothing.<Sites:Domain>`
- **THEN** the answer is 404

### Requirement: SITE-10 Request timeout
A request with no answer after `Sites:RequestTimeoutSeconds` SHALL get 504 (`site_timeout`).

#### Scenario: Slow home app
- **WHEN** the agent takes a request and does not answer in time
- **THEN** the visitor gets 504

### Requirement: SITE-11 Size limits
A visitor request body over `Sites:MaxBodyMb` SHALL get 413 and never reach the agent. An answer over `Sites:MaxBodyMb` SHALL be refused and the visitor SHALL get 502.

#### Scenario: Big upload
- **WHEN** a visitor posts a body over the limit
- **THEN** the answer is 413 and the agent gets nothing

### Requirement: SITE-12 Waiting requests per visitor address
At most `Sites:MaxPendingPerAddress` requests from one client address SHALL wait for answers at once; one more SHALL get 429.

#### Scenario: Too many at once
- **GIVEN** `Sites:MaxPendingPerAddress` requests from one address waiting
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
- **GIVEN** a builder with the site `levelwise` and its agent connected
- **WHEN** they list their sites, then delete `levelwise`
- **THEN** the list shows it online, and after the delete the list is empty and a visitor gets 404

#### Scenario: Not your site
- **WHEN** builder B deletes a site of builder A
- **THEN** the answer is 404 and the site stays

### Requirement: SITE-17 Certificate only for a real site
`GET /api/sites/tls?domain=<host>` SHALL answer 200 only when the host is `name.<Sites:Domain>` and the name is claimed, or the host is `Sites:DemoHost` and demos are on, and only to a caller on the server itself (the front proxy asks before it gets a certificate, OPS-15). Every other call SHALL answer 404.

#### Scenario: Claimed and unknown names
- **GIVEN** the claimed site `levelwise`
- **WHEN** the front proxy asks about `levelwise.<sites domain>`, then `nobody.<sites domain>`
- **THEN** the answers are 200, then 404

#### Scenario: The demo host
- **WHEN** the front proxy asks about the demo host
- **THEN** the answer is 200 when demos are on, else 404

#### Scenario: From the internet
- **WHEN** a client on the internet asks about `levelwise.<sites domain>`
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

