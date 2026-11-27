## ADDED Requirements

### Requirement: OPS-17 A version that counts up
`VERSION` at the root of the repository SHALL hold `<major>.<minor>` (start `1.1`; the owner raises it). A release of main (OPS-16) SHALL have `Dalan__Version=v<major>.<minor>.<n>` in `version.env`, `<n>` being the commits on main since `VERSION` changed, and `Dalan__Commit=<12 characters of the sha>`. `/health` (private, OPS-06) SHALL give both. The release folder SHALL keep the sha as its name.

#### Scenario: A new VERSION
- **GIVEN** the commit of main that set `VERSION` to `1.1`
- **WHEN** the server builds and deploys it
- **THEN** the version is `v1.1.0`, and the release folder is named by the sha

#### Scenario: Every update counts up
- **GIVEN** the server runs `v1.1.0`
- **WHEN** the next commit of main is deployed
- **THEN** the version is `v1.1.1`

#### Scenario: Local run
- **WHEN** Dalan runs with no `version.env`
- **THEN** `/health` gives `version` `dev` and `commit` ""
