# Spec Delta

## ADDED Requirements

### Requirement: CLI-60 Any usual phone format works
Before it sends a phone number (SMS login, password login, development login, verify your mobile), the client SHALL turn an Iranian mobile written in any usual way into `09XXXXXXXXX`: with `+98`, `0098`, `98` or no `0` at the start; with spaces, dashes, dots or brackets; with Persian or Arabic digits (CLI-12); with invisible direction marks. Other text SHALL only get Latin digits, so the server's answer stays the same.

#### Scenario: Copied from contacts
- **WHEN** the user types `+98 914 914 9202`, `0098-914-914-9202`, `(0914) 914 9202` or `9149149202`
- **THEN** the client sends `09149149202`

#### Scenario: Not an Iranian mobile
- **WHEN** the user types `+1 555 123 4567` as a phone
- **THEN** the client sends it as typed, and the server refuses it as before
