## MODIFIED Requirements

### Requirement: DEL-04 Deploy with automatic rollback
The deploy SHALL copy the release to a new folder, sending only the parts that differ from the current release, point the current link to it, restart the service, and wait for `/ready`. If `/ready` is not 200 within the configured wait, it SHALL point the link back to the previous release, restart, and exit with an error.

#### Scenario: Healthy release
- **WHEN** the new release answers `/ready` with 200 in time
- **THEN** the current link points to the new release and the deploy exits with success

#### Scenario: Broken release
- **WHEN** the new release does not answer `/ready` with 200 in time
- **THEN** the current link points to the previous release, the service is restarted, and the deploy exits with an error

#### Scenario: Only changes are sent
- **GIVEN** the current release has the same `Dalan.Host` (content and time) as the new one and another `appsettings.json`
- **WHEN** the new release is deployed
- **THEN** its `Dalan.Host` is a hard link to the current one, its `appsettings.json` is the new file, and the current release is not changed
