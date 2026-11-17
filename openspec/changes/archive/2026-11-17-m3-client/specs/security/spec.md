## MODIFIED Requirements

### Requirement: SEC-02 Rate limit per IP
The server SHALL serve each client IP address at most `Dalan:IpRequestLimit` calls in each window of `Dalan:IpLimitWindowSeconds` (start: 100 calls per 1 second) on every endpoint of every service, counted before any token or session is checked. Static files of the client and the probes do not count. A call over the limit SHALL get HTTP 429 with error code `rate_limited` and a `Retry-After` header. The client address SHALL be the one the front proxy reports in `X-Forwarded-For`.

#### Scenario: Over the limit
- **GIVEN** the limit is 2 calls per window
- **WHEN** one address makes 3 calls in the same window, with or without a token
- **THEN** the third gets 429 `rate_limited`

#### Scenario: Two addresses behind the proxy
- **GIVEN** one address is over its limit
- **WHEN** a call arrives through the proxy from another address
- **THEN** that call is served

#### Scenario: Client API and Identity are covered
- **GIVEN** the limit is 2 calls per window
- **WHEN** one address makes 3 calls to `/api/me`
- **THEN** the third gets 429 `rate_limited`

## ADDED Requirements

### Requirement: SEC-03 Rate limit per user
The Client API SHALL serve each user at most `Relay:UserRequestLimit` calls in each window of `Relay:RateLimitWindowSeconds` (start: 30 calls per 1 second; opening one screen takes about 6 calls). A call over the limit SHALL get HTTP 429 with error code `rate_limited` and a `Retry-After` header. One user's calls SHALL NOT use up another user's limit.

#### Scenario: Over the limit
- **GIVEN** the limit is 2 calls per window
- **WHEN** a user makes 3 Client API calls in the same window
- **THEN** the third gets 429 `rate_limited` with `Retry-After`

#### Scenario: Another user
- **GIVEN** one user is over the limit
- **WHEN** another user makes a call
- **THEN** that call is served

### Requirement: SEC-04 The page loads only from its own origin
Every response SHALL carry the headers `Content-Security-Policy: default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'`, `X-Content-Type-Options: nosniff` and `Referrer-Policy: same-origin`, so the browser itself refuses to load anything from a foreign host.

#### Scenario: Open the site
- **WHEN** a browser asks for `/`
- **THEN** the response has the three headers

#### Scenario: API answer
- **WHEN** a client calls an API endpoint
- **THEN** the response has the three headers

### Requirement: SEC-05 Another site cannot act as the user
A request that another site makes in the user's browser SHALL NOT change anything. The session cookie is `SameSite=Strict` (AUTH-03), and every Client API and Identity call that changes data SHALL accept only a JSON body (`Content-Type: application/json`).

#### Scenario: Form post from another site
- **GIVEN** a logged-in user
- **WHEN** a message is posted as an HTML form (`application/x-www-form-urlencoded`)
- **THEN** the response is 400 `bad_request` and nothing is stored
