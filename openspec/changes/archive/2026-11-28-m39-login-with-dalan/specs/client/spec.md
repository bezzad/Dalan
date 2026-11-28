## ADDED Requirements

### Requirement: CLI-97 Login with Dalan for builders
Each site in My apps SHALL have "Login with Dalan": a switch, the client id, a "New secret" button that shows the secret once with Copy, the discovery address, and a switch "Require login on the proxy" (SITE-26). `/guide/login`, in both languages, SHALL explain both ways step by step with the addresses from the public settings and short examples (Node, Python, PHP, PocketBase); `/llms.txt` SHALL have the same.

#### Scenario: Turn on in My apps
- **WHEN** the owner of `shop` turns on Login with Dalan
- **THEN** the card shows client id `shop`, the secret once with Copy, and the discovery address

#### Scenario: The guide
- **WHEN** a visitor opens `/guide/login` in Persian
- **THEN** it shows both ways with this server's addresses and the examples
