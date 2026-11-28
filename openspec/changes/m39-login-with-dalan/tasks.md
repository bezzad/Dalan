# Tasks

Owner asked 2026-11-27; choice: both ways, OpenID Connect first. Failing tests with the rule IDs first; CI on GitHub (rule 11).

- [x] 1. Identity: clients per site, secret hash, return address rule, audit (AUTH-40)
- [x] 2. Authorize with PKCE, login and consent screens, codes (AUTH-41)
- [x] 3. Token, ID token (ES256 key from configuration), userinfo, discovery, JWKS (AUTH-42)
- [x] 4. Per-site `sub`, no phone, email only when ticked, the list in Login and security, access log (AUTH-43)
- [x] 5. Proxy login: cookie, callback, headers, logout (SITE-26)
- [x] 6. Client: My apps card, consent screen, `/guide/login`; `/llms.txt`; docs (CLI-97)
- [ ] 7. DESIGN section 18 settings; a test with a real OpenID Connect library; CI green, final review, merge, archive
