## MODIFIED Requirements

### Requirement: OPS-07 Schema is applied at start
At start each service SHALL apply its own database migrations, in its own schema. While the database is not reachable the server SHALL keep running and try again every `Dalan:StartupRetrySeconds` (start 5).

#### Scenario: Empty database
- **WHEN** the server starts on an empty database
- **THEN** the schemas `identity`, `relay` and `admin` exist, each with its own migration history table, and `/ready` answers 200

#### Scenario: Start again
- **WHEN** the server starts on a database that already has the newest schema
- **THEN** it becomes ready and the data is unchanged
