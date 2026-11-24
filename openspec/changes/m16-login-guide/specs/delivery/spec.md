## ADDED Requirements

### Requirement: DEL-09 Installs take nothing from a foreign host
`install.sh`, `install.ps1` and `dalan.mjs` (the files a user runs) SHALL name no host except the Dalan address given to them (`__DALAN_URL__`, `DALAN_URL`): no GitHub, npm, NuGet, CDN or any other address. The agent SHALL have no default server except `dalanet.ir`. The server's own setup (`deploy/install.sh`) MAY reach foreign hosts once, and the milestone summary SHALL list them.

#### Scenario: User scripts
- **WHEN** the three files are scanned for `http://` and `https://` addresses
- **THEN** none is found except the `__DALAN_URL__` placeholder
