# Spec Delta

## ADDED Requirements

### Requirement: OPS-15 Front proxy for sites and the tunnel
The front proxy SHALL serve the tunnel host (`DALAN_TUNNEL_DOMAIN`, default `tunnel.<site domain>`) like the site domain. Every other HTTPS host SHALL get its certificate on its first visit, only after Dalan says yes (SITE-17), and SHALL go to Dalan with the client's real address; none of its paths is blocked, because they are the user's site. `setup.sh` SHALL write the tunnel host, `Sites__TunnelUrl` and `Sites__Domain` (from `SITES_DOMAIN`; empty: sites are off) into `domains.env` on each run.

#### Scenario: Unknown host
- **WHEN** a client opens `https://shop.<sites domain>` and Dalan has no site `shop`
- **THEN** the proxy asked Dalan and gets no certificate, so the connection fails

#### Scenario: Sites domain from install.sh
- **WHEN** the owner runs `SITES_DOMAIN=dalancloud.ir deploy/install.sh ...`
- **THEN** `domains.env` has `Sites__Domain=dalancloud.ir` and setup prints the DNS record `*.dalancloud.ir`
