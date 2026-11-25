# Security

## ADDED Requirements

### Requirement: SEC-16 The view host cannot touch the app
The view host (REL-57) SHALL answer only `GET /v/{token}`; every other path SHALL be 404. A valid, unexpired token whose user may still get the file (REL-29) SHALL return the file's bytes with `Content-Type: text/html; charset=utf-8`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Cache-Control: private, no-store` and `Content-Security-Policy: sandbox allow-scripts allow-popups; default-src 'none'; script-src 'unsafe-inline' data: blob:; style-src 'unsafe-inline' data: blob:; img-src data: blob:; font-src data:; media-src data: blob:; connect-src 'none'; form-action 'none'; base-uri 'none'; frame-ancestors <app origin>`. It SHALL never read or set a cookie. A bad or expired token, or a file the user may no longer get, SHALL answer 404. Each answer SHALL be in the access log (ADM-01) with the token's user. The app's CSP SHALL allow only the view host in `frame-src`. The certificate ask (SITE-17) SHALL say yes for the view host, and `view` SHALL be in the default `Sites:ReservedNames`.

#### Scenario: Headers
- **GIVEN** a fresh view link for `report.html`
- **WHEN** it is opened
- **THEN** the body is the file, the CSP has `sandbox allow-scripts allow-popups` without `allow-same-origin`, `connect-src 'none'` and `frame-ancestors` with only the app origin, and the answer has no `Set-Cookie`

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
