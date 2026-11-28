# m39: Login with Dalan for the users' sites

## Why
Owner (2026-11-27): many sites served through Dalan want their users to log in with Dalan: the site sends the visitor to
Dalan's login page, and the visitor comes back to the site's own `*.dalans.ir` address. Today a site cannot: "Login with
Dalan" and the signed user header were left out of the site milestones. Owner's choice: both ways, OpenID Connect first,
then a login on the proxy (like ngrok's) built on it. Full documentation for builders.

## What changes
- AUTH-40: a builder turns on "Login with Dalan" for an own site and gets a client id and a secret (shown once).
- AUTH-41: the authorize step (code with PKCE), Dalan's login when needed, a consent screen once per site.
- AUTH-42: the token step, the ID token, userinfo, discovery and keys: standard OpenID Connect, so every library works.
- AUTH-43: privacy: never the phone; a different user id for each site; email only when the user agrees; the user sees
  and removes the sites in Login and security.
- SITE-26: the proxy login: a site can ask Dalan to require login before any request, and gets the user in headers.
- CLI-97: the builder's switch and the guide page `/guide/login` with examples; `/llms.txt`.

## Defaults chosen (owner reviews the spec)
No refresh tokens (a site keeps its own session). The redirect address must be on the site's own host
(`https://<name>.<Sites:Domain>/...`), or `http://localhost` for a builder's tests. Settings: `Identity:OAuthCodeSeconds`
(60), `Identity:OAuthTokenMinutes` (10), `Sites:LoginCookieHours` (24).
