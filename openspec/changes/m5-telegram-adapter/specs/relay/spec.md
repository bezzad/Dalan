## ADDED Requirements

### Requirement: REL-19 One contract for every bot door
Relay SHALL offer what a bot may do as the contract `IBotGateway` in `Dalan.Relay.Contracts`: enter with a token (REL-04, SEC-01), get updates, send a message. The core Bot API and every adapter plugin SHALL use this contract, so both doors share one queue and one set of rules.

#### Scenario: Read on one door, confirm on the other
- **GIVEN** the bot got an update through the core Bot API
- **WHEN** it confirms it through the Telegram adapter with the next offset
- **THEN** the message is marked "delivered" and no door returns it again

#### Scenario: Send on one door, same chat
- **GIVEN** a user who started the bot
- **WHEN** the bot sends one message through the core Bot API and one through the Telegram adapter
- **THEN** the user's history has both, with sequence numbers that follow each other
