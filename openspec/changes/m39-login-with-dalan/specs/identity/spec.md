## ADDED Requirements

### Requirement: AUTH-40 A site's login client
The owner of a site (SITE-16) SHALL turn on "Login with Dalan" for it and get a client id (the site name) and a secret, shown once and kept only as a hash; a new secret SHALL replace the old one. Allowed return addresses SHALL be on the site's own host, `https://<name>.<Sites:Domain>/...`, or `http://localhost:<port>/...` for tests. Turning it off, or deleting the site, SHALL end it. Each change SHALL be in the audit log.

#### Scenario: Turn on
- **WHEN** the owner of `shop` turns on Login with Dalan
- **THEN** the answer has client id `shop` and a secret, once; the list shows it on, with no secret

#### Scenario: A foreign return address
- **WHEN** a login asks to return to `https://other.dalans.ir/cb` with client `shop`
- **THEN** it is refused on Dalan's page and nothing goes to that address

### Requirement: AUTH-41 Authorize
`GET /oauth/authorize` with `response_type=code`, `client_id`, `redirect_uri`, `scope` (`openid`, optional `profile` and `email`), `state`, `nonce` and a PKCE `code_challenge` (S256, required) SHALL show Dalan's login when there is no session, then, the first time for that site, a consent screen naming the site and what it gets. On Allow it SHALL return to `redirect_uri` with `code` (one use, `Identity:OAuthCodeSeconds`, start 60) and `state`; on Deny with `error=access_denied`.

#### Scenario: First login on a site
- **GIVEN** Sara is logged in on Dalan and never used `shop`
- **WHEN** `shop` sends her to authorize with `openid profile`
- **THEN** she sees the consent screen with `shop.dalans.ir` and her name; Allow returns her to the site with a code and the same `state`

#### Scenario: No PKCE
- **WHEN** the request has no `code_challenge`
- **THEN** it returns to the site with `error=invalid_request`

### Requirement: AUTH-42 Token, user info and discovery
`POST /oauth/token` (`authorization_code`, the client secret by Basic auth or form, `code_verifier`) SHALL give an ID token (signed JWT, ES256, `iss` the site URL, `aud` the client, `sub`, `nonce`, the agreed claims) and an access token for `GET /oauth/userinfo`, both `Identity:OAuthTokenMinutes` (start 10). `/.well-known/openid-configuration` and its JWKS SHALL describe it. A wrong secret, verifier or used code: 400 `invalid_grant`.

#### Scenario: Code for tokens
- **GIVEN** a code from AUTH-41
- **WHEN** the site sends it with its secret and verifier
- **THEN** the answer has an ID token whose signature checks with the JWKS key and whose `aud` is `shop`

#### Scenario: Code used twice
- **WHEN** the same code is sent again
- **THEN** the answer is 400 `invalid_grant`

### Requirement: AUTH-43 What a site may know
A site SHALL NOT get the phone number. Its `sub` SHALL be a stable id made for that site, so two sites cannot match their users. `name` and `preferred_username` come with `profile`; `email` and `email_verified` only with `email` and when the user ticked it on the consent screen. "Login and security" SHALL list the sites the user allowed; removing one SHALL make the next login ask again. Every authorize and token call SHALL be in the access log.

#### Scenario: Two sites
- **WHEN** Sara logs in on `shop` and on `blog`
- **THEN** the two `sub` values differ, and neither site gets her phone

#### Scenario: Email not ticked
- **GIVEN** `shop` asks for `email` and Sara leaves it unticked
- **WHEN** the site reads the ID token and userinfo
- **THEN** there is no `email`
