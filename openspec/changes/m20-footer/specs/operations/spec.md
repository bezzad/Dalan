## ADDED Requirements

### Requirement: OPS-17 Version from the commit
When the server builds a commit of main (OPS-16), the release's `version.env` SHALL hold `Dalan__Version=v<yy.mm.dd.HHMMSS>`, the commit time in Tehran time, and `Dalan__Commit=<the first 12 characters of the sha>`. `/health` SHALL give both as `version` and `commit`. Without them (a local run) `version` SHALL be `dev` and `commit` empty. The release folder SHALL keep the 12-character sha as its name.

#### Scenario: Built from main
- **GIVEN** the commit `7207d600e166…` of main was made at 2026-11-25 11:47:26 Tehran time
- **WHEN** the server builds and deploys it
- **THEN** `/health` gives `version` `v26.11.25.114726` and `commit` `7207d600e166`, and the release folder is `releases/7207d600e166`

#### Scenario: Local run
- **WHEN** Dalan runs with no `version.env`
- **THEN** `/health` gives `version` `dev` and `commit` ""
