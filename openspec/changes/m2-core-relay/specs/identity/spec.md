## ADDED Requirements

### Requirement: AUTH-01 Identity owns the users
Identity SHALL keep users in the PostgreSQL schema `identity`. A user has a numeric id that never changes, a phone number, a display name and an optional username. A phone number SHALL belong to one user only, and a username SHALL be unique without regard to letter case.

#### Scenario: Schema after start
- **WHEN** the server has started on an empty database
- **THEN** the table `identity.users` exists

#### Scenario: Same phone number twice
- **GIVEN** a user with phone number `+989121234567`
- **WHEN** a second user with the same number is saved
- **THEN** the save fails

#### Scenario: Same username in another case
- **GIVEN** a user with username `Sara`
- **WHEN** a second user with username `sara` is saved
- **THEN** the save fails

### Requirement: AUTH-02 Other services learn only the public profile
Other services SHALL read a user only through the contract `IUserDirectory`, which gives the user's id, display name and username. The contract SHALL NOT carry the phone number.

#### Scenario: Known user
- **WHEN** a service asks for an existing user
- **THEN** it gets the id, display name and username

#### Scenario: Unknown user
- **WHEN** a service asks for a user id that does not exist
- **THEN** it gets nothing

#### Scenario: No phone in the contract
- **WHEN** the types of `Dalan.Identity.Contracts` are inspected
- **THEN** none has a member with "phone" in its name
