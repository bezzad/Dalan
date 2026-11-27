## ADDED Requirements

### Requirement: CLI-79 Calendar choice
Preferences (CLI-77) SHALL let the user show dates in the Persian (Jalali) or the Gregorian calendar, as two radio cards with today's date as the example. The choice SHALL apply at once to every date the client shows and be kept on the device. With no choice, Persian SHALL use Jalali and English Gregorian.

#### Scenario: Default
- **WHEN** a key's expiry is shown in Persian with no choice made
- **THEN** it is a Jalali date in Persian digits

#### Scenario: Gregorian in Persian
- **GIVEN** the user chose Gregorian
- **WHEN** the same date is shown in Persian
- **THEN** it is a Gregorian date with Persian month names and digits
