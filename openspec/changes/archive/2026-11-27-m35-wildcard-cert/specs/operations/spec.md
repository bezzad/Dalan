## ADDED Requirements

### Requirement: OPS-18 One certificate for every site
With `ARVAN_API_KEY` given to setup, the server SHALL get one Let's Encrypt certificate for `*.<sites domain>` by a DNS challenge at ArvanCloud (lego), check it daily (`dalan-cert.timer`), renew it 30 days before its end, and restart Caddy only when a certificate was made or renewed. The key SHALL be in a file only root reads. Once the certificate exists Caddy SHALL use it for every site host; before that, or with no key, each site gets its own certificate (OPS-15).

#### Scenario: First certificate
- **GIVEN** setup ran with `ARVAN_API_KEY` and `SITES_DOMAIN=dalans.ir`
- **WHEN** the timer runs and lego makes the certificate
- **THEN** Caddy gets the certificate and key, its site block for `*.dalans.ir` uses them, and Caddy restarts once

#### Scenario: Nothing due
- **GIVEN** the certificate has more than 30 days left
- **WHEN** the timer runs
- **THEN** nothing is copied and Caddy does not restart

#### Scenario: No key
- **WHEN** setup runs without `ARVAN_API_KEY`
- **THEN** no timer is enabled and each site gets its own certificate as before

#### Scenario: A new site in a shutdown
- **GIVEN** Caddy has the wildcard certificate
- **WHEN** a visitor opens a site host Caddy never saw
- **THEN** the handshake uses the wildcard certificate and Caddy asks no certificate authority
