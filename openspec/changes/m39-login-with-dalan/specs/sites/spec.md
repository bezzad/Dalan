## ADDED Requirements

### Requirement: SITE-26 Login on the proxy
A site owner MAY require Dalan login on the proxy (in My apps, or `dalan http --login`). Then a visitor with no login cookie SHALL be sent through AUTH-41 (Dalan's own client) and back to `/.dalan/callback` on the site host, which sets a host-only cookie (HttpOnly, Secure, `Sites:LoginCookieHours`, start 24). Each request SHALL reach the site with `X-Dalan-User` (AUTH-43 `sub`) and `X-Dalan-Name`; the visitor's own headers of these names SHALL be dropped. `/.dalan/logout` SHALL end it.

#### Scenario: A visitor with no login
- **GIVEN** `shop` requires login
- **WHEN** a visitor opens `https://shop.dalans.ir/cart`
- **THEN** they go to Dalan's login and consent, then back to `/cart`, and the site gets `X-Dalan-User` and `X-Dalan-Name`

#### Scenario: Forged header
- **WHEN** a visitor sends their own `X-Dalan-User`
- **THEN** the site never sees it
