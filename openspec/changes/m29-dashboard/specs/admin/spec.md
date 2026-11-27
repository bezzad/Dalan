## ADDED Requirements

### Requirement: ADM-19 Described settings with ranges
`GET /api/admin/settings` SHALL give, with each setting, its group, name and help in Persian and English, its unit, its `min` and `max` for a number, and `risky`, from a description file kept with the settings catalog. A setting with no description SHALL still be listed, with its key as the name. `PUT` of a number outside its range SHALL get 400 `out_of_range` and change nothing. `GET /api/admin/overview` SHALL give the counts of CLI-82. Both SHALL need an admin (ADM-05).

#### Scenario: Described
- **WHEN** an admin reads the settings
- **THEN** `Admin:AccessLogRetentionDays` has group "Admin", a name and help in both languages, unit "days" and a `min` and `max`

#### Scenario: Out of range
- **GIVEN** `Admin:AccessLogRetentionDays` has `min` 30
- **WHEN** an admin sets it to 5
- **THEN** the answer is 400 `out_of_range` and the value stays

#### Scenario: Every key described
- **WHEN** the tests list the catalog's keys
- **THEN** each has a description, or the test names the missing ones
