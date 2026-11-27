# m35: one wildcard certificate for the users' sites

## Why
Owner (2026-11-27), thinking of a day when Iran's internet is cut: each user site now gets its own Let's Encrypt
certificate on its first visit (OPS-15). With no way out, a site made then has no HTTPS at all. One certificate for
`*.<sites domain>` covers every site, also new ones, until it expires. Owner's choice: Let's Encrypt with a DNS
challenge at ArvanCloud.

## What changes
- OPS-18: with an ArvanCloud API key, the server gets and renews `*.<sites domain>` with lego (a single file, like
  Caddy, downloaded by install.sh with its checksum). Caddy uses it for every site host. Without the key nothing changes.

## Impact
`deploy/install.sh`, `deploy/setup.sh`, `deploy/Caddyfile`, `deploy/caddy.service`, new `deploy/wildcard-cert.sh` and
`dalan-cert` units; tests in `tests/deploy` and `tests/proxy/caddy_test.sh`; `docs/SERVER.md`. No product setting.
Limit: a certificate still expires (90 days, renewed at 30 left), so a longer shutdown still ends HTTPS.
