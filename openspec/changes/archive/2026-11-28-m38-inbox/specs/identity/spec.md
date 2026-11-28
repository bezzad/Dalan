## MODIFIED Requirements

### Requirement: AUTH-02 Other services learn only the public profile
Other services SHALL read a user only through the contract `IUserDirectory`, which gives the user's id, display name and username, what the user may do (admin, builder, suspended), and the ids of the admins (SITE-25). The contract SHALL NOT carry the phone number. Only the Admin service SHALL use `IUserAdmin`, which carries it for legal complaints (ADM-11). Only the Notification service SHALL use `IUserPhones`, which gives one user's phone for a notice SMS (NTF-13).

#### Scenario: Known user
- **WHEN** a service asks for an existing user
- **THEN** it gets the id, display name and username

#### Scenario: Unknown user
- **WHEN** a service asks for a user id that does not exist
- **THEN** it gets nothing

#### Scenario: No phone in the contract
- **WHEN** the types of `Dalan.Identity.Contracts` other than `IUserAdmin`, `AdminUser` and `IUserPhones` are inspected
- **THEN** none has a member with "phone" in its name

#### Scenario: Admin ids
- **GIVEN** `Identity:AdminPhones` lists the phone of one user
- **WHEN** a service asks for the admins
- **THEN** it gets that user's id and no other
