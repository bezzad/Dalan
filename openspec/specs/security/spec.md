# security Specification

## Purpose
Limits that protect public endpoints from floods and abuse.

## Requirements

### Requirement: SEC-01 Rate limit per bot
The Bot API SHALL serve each bot at most `Relay:BotRequestLimit` calls in each window of `Relay:RateLimitWindowSeconds` (start: 30 calls per 1 second). A call over the limit SHALL get HTTP 429 with error code `rate_limited` and a `Retry-After` header. One bot's calls SHALL NOT use up another bot's limit, and calls with a wrong token SHALL NOT use up a bot's limit.

#### Scenario: Over the limit
- **GIVEN** the limit is 2 calls per window
- **WHEN** a bot makes 3 calls in the same window
- **THEN** the third gets 429 `rate_limited` with `Retry-After`

#### Scenario: Another bot
- **GIVEN** one bot is over its limit
- **WHEN** another bot makes a call
- **THEN** that call is served

#### Scenario: Wrong token
- **GIVEN** many calls with a bot's id and a wrong secret
- **WHEN** the real bot makes a call
- **THEN** that call is served

### Requirement: SEC-02 Rate limit per IP
The server SHALL serve each client IP address at most `Dalan:IpRequestLimit` calls in each window of `Dalan:IpLimitWindowSeconds` (start: 100 calls per 1 second) on every endpoint of every service, counted before any token or session is checked. Static files of the client and the probes do not count. A call over the limit SHALL get HTTP 429 with error code `rate_limited` and a `Retry-After` header. The client address SHALL be the real client address of SEC-10.

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

#### Scenario: A faked header does not open a new limit
- **GIVEN** the limit is 2 calls per window and a caller that is not a trusted proxy
- **WHEN** it makes 3 calls, each with a different `X-Forwarded-For`
- **THEN** the third gets 429 `rate_limited`

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
Every response SHALL carry the headers `Content-Security-Policy: default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'`, `X-Content-Type-Options: nosniff` and `Referrer-Policy: same-origin`, so the browser itself refuses to load anything from a foreign host. While the file viewer is on (REL-57), the CSP SHALL also have `frame-src` with only the view host (SEC-16).

#### Scenario: Open the site
- **WHEN** a browser asks for `/`
- **THEN** the response has the three headers

#### Scenario: API answer
- **WHEN** a client calls an API endpoint
- **THEN** the response has the three headers

#### Scenario: Viewer on
- **GIVEN** the view host is `view.dalans.ir`
- **WHEN** a browser asks for `/`
- **THEN** the CSP also has `frame-src https://view.dalans.ir` and no other frame source

### Requirement: SEC-05 Another site cannot act as the user
A request that another site makes in the user's browser SHALL NOT change anything. The session cookie is `SameSite=Strict` (AUTH-03), and every Client API and Identity call that changes data SHALL accept only a JSON body (`Content-Type: application/json`). The one exception, the file upload (REL-28), SHALL need the header `X-File-Name`, which a form of another site cannot send.

#### Scenario: Form post from another site
- **GIVEN** a logged-in user
- **WHEN** a message is posted as an HTML form (`application/x-www-form-urlencoded`)
- **THEN** the response is 400 `bad_request` and nothing is stored

#### Scenario: Upload without the header
- **GIVEN** a logged-in user
- **WHEN** a file is posted to the upload address as `multipart/form-data` without `X-File-Name`
- **THEN** the response is 400 `bad_request` and nothing is stored

### Requirement: SEC-06 Proof of work before each SMS
`POST /api/auth/challenge` with `phone` or `email` SHALL return a `challenge` and a `difficulty` (`Identity:PowDifficultyBits`, start 14). An SMS or a login email SHALL be sent only with that challenge and a `nonce` such that the SHA-256 hash of the text `<challenge>.<nonce>` starts with at least `difficulty` zero bits. A challenge SHALL work only for its phone or email and for `Identity:PowChallengeSeconds` (start 120). Any other case SHALL get 400 `bad_proof` and send nothing.

#### Scenario: Get a challenge
- **WHEN** a client posts a mobile number to `POST /api/auth/challenge`
- **THEN** the response is 200 with a `challenge` and `difficulty` 14

#### Scenario: Challenge for an email
- **WHEN** a client posts an email to `POST /api/auth/challenge`
- **THEN** the response is 200 with a `challenge`, and the solved challenge sends a code to that email only

#### Scenario: Known hash
- **WHEN** the challenge `abc` and the nonce `0` are checked (SHA-256 of `abc.0` starts with the byte `20`, which is 2 zero bits)
- **THEN** the proof passes difficulty 2 and fails difficulty 3

#### Scenario: No work done
- **WHEN** a client asks an SMS with a real challenge and a nonce that does not give the zero bits
- **THEN** the response is 400 `bad_proof` and no SMS is sent

#### Scenario: Challenge of another phone
- **WHEN** a client solves the challenge of one phone and asks an SMS for another phone
- **THEN** the response is 400 `bad_proof`

#### Scenario: Old challenge
- **WHEN** a client sends a solved challenge after `Identity:PowChallengeSeconds`
- **THEN** the response is 400 `bad_proof`

#### Scenario: Made-up challenge
- **WHEN** a client sends a challenge the server did not make
- **THEN** the response is 400 `bad_proof`

### Requirement: SEC-07 SMS limits per phone number
The server SHALL send at most one SMS to a number in each `Identity:SmsWaitSeconds` (start 120); after `Identity:SmsPerPhonePerDay` (start 5) in any 24 hours, in each `Identity:SmsLongWaitSeconds` (start 1800). Numbers in `Identity:AdminPhones` SHALL never get the long wait. Too soon SHALL get 429 `sms_wait` with `Retry-After` (seconds left). A sent code's answer SHALL have `retry_in`, the seconds before the next one. Numbers SHALL NOT share limits.

#### Scenario: Second SMS too soon
- **GIVEN** an SMS went to a number 20 seconds ago and the wait is 60
- **WHEN** a client asks another one for the same number, with a valid proof
- **THEN** the response is 429 `sms_wait` with `Retry-After: 40` and no SMS is sent

#### Scenario: After the wait
- **GIVEN** an SMS went to a number 60 seconds ago and the wait is 60
- **WHEN** a client asks another one
- **THEN** the SMS is sent

#### Scenario: Daily limit
- **GIVEN** 5 SMS went to a number in the last 24 hours, the last one 2 minutes ago
- **WHEN** a client asks a sixth
- **THEN** the response is 429 `sms_wait` with `Retry-After: 1680`, and after that time the SMS is sent

#### Scenario: The fifth SMS says the long wait
- **WHEN** the fifth SMS in 24 hours is sent to a number
- **THEN** its answer has `retry_in` 1800

#### Scenario: A day later
- **GIVEN** a number that got 5 SMS, the last one more than 24 hours ago
- **WHEN** a client asks an SMS
- **THEN** the SMS is sent, and its `retry_in` is the short wait

#### Scenario: Admin number
- **GIVEN** the number is in `Identity:AdminPhones` and got 5 SMS in the last 24 hours
- **WHEN** a client asks a sixth after the short wait
- **THEN** the SMS is sent

#### Scenario: Another number
- **GIVEN** one number is waiting
- **WHEN** a client asks an SMS for another number
- **THEN** the SMS is sent

### Requirement: SEC-08 SMS limit per address and the daily budget
The server SHALL send at most `Identity:SmsPerIpPerDay` (start 200) SMS for calls from one client address in any 24 hours; over it the answer SHALL be 429 `sms_limit` with a `Retry-After` header: the seconds until the oldest of those SMS is 24 hours old. The server SHALL send at most `Identity:SmsPerDay` (start 1000) SMS in total in any 24 hours; over it the answer SHALL be 503 `sms_unavailable` and the server SHALL write an error log line, so the admin sees it.

#### Scenario: Address limit
- **GIVEN** the limit per address is 2 and one address got 2 SMS sent to two numbers, the first one an hour ago
- **WHEN** the same address asks an SMS for a third number
- **THEN** the response is 429 `sms_limit` with `Retry-After: 82800`

#### Scenario: Another address
- **GIVEN** one address is over its limit
- **WHEN** another address asks an SMS
- **THEN** the SMS is sent

#### Scenario: Budget is used up
- **GIVEN** the daily budget is 2 and 2 SMS were sent
- **WHEN** any client asks an SMS for a new number
- **THEN** the response is 503 `sms_unavailable`, no SMS is sent, and an error is logged

### Requirement: SEC-09 Password tries are limited
`POST /api/auth/password` SHALL serve at most `Identity:PasswordTries` (start 5) tries for one login name, without regard to letter case, and at most `Identity:PasswordTriesPerIp` (start 100) tries from one client address, in each window of `Identity:PasswordTriesWindowMinutes` (start 15). A try over a limit SHALL get 429 `rate_limited` with a `Retry-After` header, even with the right password. One login name's tries SHALL NOT use up another's.

#### Scenario: Over the limit
- **GIVEN** the limit is 2 tries
- **WHEN** a client posts 2 wrong passwords and then the right one for the same login
- **THEN** the third answer is 429 `rate_limited` with `Retry-After`

#### Scenario: Another login
- **GIVEN** one login is over its limit
- **WHEN** another user logs in with the right password
- **THEN** the response is 200

#### Scenario: One address, many logins
- **GIVEN** the limit per address is 2
- **WHEN** one address tries 3 different login names
- **THEN** the third answer is 429 `rate_limited`, and a try from another address is served

### Requirement: SEC-10 Real client address
The server SHALL take the client's address and source port from `X-Forwarded-For` only when the connection comes from an address in `Dalan:TrustedProxies` (addresses or CIDR ranges, comma separated; start: `127.0.0.1,::1`). It SHALL read the header from the right and stop at the first address that is not trusted; that is the client. Otherwise the client is the address of the connection itself. The rate limit and the access log SHALL use this one address.

#### Scenario: Through the trusted proxy
- **GIVEN** a trusted proxy
- **WHEN** it passes a call with `X-Forwarded-For: 203.0.113.7:51000`
- **THEN** the client address is `203.0.113.7` and the port is `51000`

#### Scenario: Faked header
- **GIVEN** a caller that is not a trusted proxy
- **WHEN** it sends `X-Forwarded-For: 203.0.113.7`
- **THEN** the client address is the caller's own address

#### Scenario: Load balancer in front
- **GIVEN** the load balancer `10.0.0.5` is in `Dalan:TrustedProxies`
- **WHEN** a call arrives from it with `X-Forwarded-For: 1.1.1.1, 198.51.100.9`, where the client wrote `1.1.1.1` and the balancer added `198.51.100.9`
- **THEN** the client address is `198.51.100.9`

#### Scenario: Two proxies in a row
- **GIVEN** both `10.0.0.5` and `10.0.0.6` are trusted
- **WHEN** a call arrives from `10.0.0.6` with `X-Forwarded-For: 198.51.100.9, 10.0.0.5`
- **THEN** the client address is `198.51.100.9`

#### Scenario: Front proxy
- **WHEN** a client sends `X-Forwarded-For: 9.9.9.9` to the front proxy
- **THEN** the server gets `X-Forwarded-For` with only the client's real `address:port`

### Requirement: SEC-11 Warning for a proxy that is not listed
When a call that carries `X-Forwarded-For` comes from a private or loopback address that is not in `Dalan:TrustedProxies`, the server SHALL log a warning that names the address and the setting, once for each such address. This is the sign of a proxy or load balancer that was added without the setting, which would make every client look like the proxy.

#### Scenario: Forgotten load balancer
- **GIVEN** `10.0.0.5` is not in `Dalan:TrustedProxies`
- **WHEN** two calls with `X-Forwarded-For` arrive from `10.0.0.5`
- **THEN** one warning names `10.0.0.5` and `Dalan:TrustedProxies`

#### Scenario: Public caller
- **WHEN** a call with `X-Forwarded-For` arrives from the public address `198.51.100.9`
- **THEN** no warning is logged

### Requirement: SEC-12 A file cannot run in the site
Every file download (REL-29) SHALL carry its stored content type, `X-Content-Type-Options: nosniff` and `Content-Security-Policy: sandbox`. It SHALL carry `Content-Disposition: attachment` unless its type is `image/jpeg`, `image/png`, `image/gif`, `image/webp`, `audio/*` or `video/*`, which are shown inline.

#### Scenario: HTML file
- **GIVEN** a bot sent a file `page.html` of type `text/html`
- **WHEN** the user downloads it
- **THEN** the response has `Content-Disposition: attachment`, `nosniff` and a `sandbox` policy

#### Scenario: Photo
- **GIVEN** a bot sent a PNG photo
- **WHEN** the user downloads it
- **THEN** the response has type `image/png` and is not an attachment

### Requirement: SEC-13 Email code limits
Email codes SHALL follow SEC-07 with `Identity:EmailWaitSeconds` (start 60), `Identity:EmailPerAddressPerDay` (start 5) and `Identity:EmailLongWaitSeconds` (start 1800); too soon gets 429 `email_wait` with `Retry-After`. Over `Identity:EmailPerIpPerDay` (start 200) per client address in 24 hours: 429 `email_limit` with `Retry-After`. Over `Identity:EmailPerDay` (start 1000) in total: 503 `email_unavailable`. Email counters are apart from SMS counters.

#### Scenario: Second email too soon
- **GIVEN** an email code went to an address 20 seconds ago
- **WHEN** a client asks another one for the same address
- **THEN** the response is 429 `email_wait` with `Retry-After: 40`

#### Scenario: Daily limit
- **GIVEN** 2 is the cap and 2 email codes went to an address, the last one 60 seconds ago
- **WHEN** a client asks a third
- **THEN** the response is 429 `email_wait` with `Retry-After: 1740`

#### Scenario: Budget is used up
- **GIVEN** the daily email budget is 1 and 1 email was sent
- **WHEN** a client asks a code for another address
- **THEN** the response is 503 `email_unavailable`, and an SMS can still be sent

### Requirement: SEC-14 The server is the gate for admin pages
Hiding admin items in the client SHALL NOT be the only guard. Every `/api/admin` call SHALL be refused for a user who is not an admin, the same way for every admin call, and the try SHALL be in the access log. No answer to a common user SHALL hold admin-only data (other users' phones or emails, settings, logs).

#### Scenario: Common user calls an admin API
- **GIVEN** a logged-in user who is not an admin
- **WHEN** the user calls `GET /api/admin/settings` and `GET /api/admin/overview`
- **THEN** both are refused, and the access log has both calls

#### Scenario: Admins only from the server
- **WHEN** every API, the admin panel and the account merge are tried in the tests
- **THEN** none can make a user an admin; only `Identity:AdminPhones` in the server's environment does, and the panel cannot read or change it

#### Scenario: Nothing leaks in common answers
- **WHEN** the answers of every non-admin API a common user can call are read in the tests
- **THEN** none holds another user's phone or email, a setting value, or a log row

### Requirement: SEC-15 Second factor for admins
An admin (SEC-14: a phone in `Identity:AdminPhones`) SHALL have the second factor (AUTH-37) on. Every login of an admin, by password, SMS, email or GitHub, SHALL also need a TOTP code or an unused recovery code before a session is made. While an admin has no second factor yet, every `/api/admin` call SHALL be refused with 403 `admin_needs_2fa`, and the dashboard SHALL show only the page to set it up. An admin SHALL NOT be able to turn the second factor off.

#### Scenario: Admin logs in by SMS
- **GIVEN** an admin with the second factor on
- **WHEN** the admin enters the right SMS code
- **THEN** the answer asks for the TOTP code, and no session is made until a right code comes

#### Scenario: No second factor yet
- **GIVEN** an admin without the second factor
- **WHEN** the admin opens `/dashboard/admin` or calls `GET /api/admin/settings`
- **THEN** the page shows only the set-up of the second factor, and the call gets 403 `admin_needs_2fa`

#### Scenario: Cannot turn it off
- **WHEN** an admin asks to turn the second factor off
- **THEN** the answer is 403 and it stays on

#### Scenario: Admin powers outside /api/admin
- **GIVEN** an admin without the second factor
- **WHEN** the admin changes another user's bot or deletes another user's site
- **THEN** it is refused as for a common user; with the second factor on, it works

#### Scenario: Turning it on ends the other sessions
- **GIVEN** an admin session made on another device before the second factor
- **WHEN** the admin turns the second factor on
- **THEN** that other session ends; only this one stays

### Requirement: SEC-16 The view host cannot touch the app
The view host (REL-57) SHALL answer only `GET /v/{token}`; every other path SHALL be 404. A valid, unexpired token whose user may still get the file (REL-29) SHALL give its bytes with the headers below. It SHALL never read or set a cookie. A bad or expired token, or a file the user may no longer get, SHALL answer 404. Each answer SHALL be in the access log with the token's user. The app CSP SHALL allow only the view host in `frame-src`; SITE-17 SHALL say yes for it; `view` SHALL be reserved.

#### Scenario: Headers
- **GIVEN** a fresh view link for `report.html`
- **WHEN** it is opened
- **THEN** the body is the file, the CSP has `sandbox allow-scripts allow-popups` without `allow-same-origin`, `connect-src 'none'` and `frame-ancestors` with only the app origin, and the answer has no `Set-Cookie`
- **AND** the headers are exactly `Content-Type: text/html; charset=utf-8`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Cache-Control: private, no-store` and `Content-Security-Policy: sandbox allow-scripts allow-popups; default-src 'none'; script-src 'unsafe-inline' data: blob:; style-src 'unsafe-inline' data: blob:; img-src data: blob:; font-src data:; media-src data: blob:; connect-src 'none'; form-action 'none'; base-uri 'none'; frame-ancestors <app origin>`

#### Scenario: Reserved name
- **WHEN** a builder claims the site name `view`
- **THEN** it is refused, because `view` is in the default `Sites:ReservedNames`

#### Scenario: Expired
- **GIVEN** a view link made 11 minutes ago
- **WHEN** it is opened
- **THEN** the answer is 404

#### Scenario: Message deleted
- **GIVEN** a view link, then the bot deletes the message that carried the file
- **WHEN** the link is opened
- **THEN** the answer is 404

#### Scenario: Other paths
- **WHEN** `https://view.dalans.ir/api/me` is opened
- **THEN** the answer is 404

#### Scenario: Nothing reaches the app
- **GIVEN** the sample report is open in the viewer, in WebKit and in Chromium
- **WHEN** its script reads `document.cookie`, `localStorage`, `parent.document`, and calls `fetch("https://dalanet.ir/api/me")`
- **THEN** each one throws, is empty or is blocked

#### Scenario: App CSP and certificate
- **WHEN** the app page is loaded and the front proxy asks for a certificate for `view.dalans.ir`
- **THEN** the app CSP has `frame-src https://view.dalans.ir`, and the ask answers 200
