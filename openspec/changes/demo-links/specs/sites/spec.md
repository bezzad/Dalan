# Spec Delta

## ADDED Requirements

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

## MODIFIED Requirements

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
