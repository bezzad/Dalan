## MODIFIED Requirements

### Requirement: CLI-30 Access keys and ready prompt
The account screen SHALL have, for an approved builder or an admin, a part for AI assistants (anchor `#keys`): the user's keys (name, made, expires) with a delete button, a form to make a key that shows the new key once with a copy button, and a ready prompt to copy. "My bots" and "My apps" SHALL NOT show this part; each SHALL have one link "Access keys for AI assistants" to `/account#keys`. The ready prompt SHALL name the site address, the Bot API address, `/llms.txt`, the MCP door and the CLI, with the addresses from the public settings (ADM-15), and SHALL never hold a key.

#### Scenario: Ready prompt
- **GIVEN** the public settings say site `https://dalanet.ir` and Bot API `https://api.dalanet.ir`
- **WHEN** the ready prompt is made in Persian and in English
- **THEN** it has `https://dalanet.ir/llms.txt`, `https://dalanet.ir/api/mcp`, `https://dalanet.ir/dalan.mjs` and `https://api.dalanet.ir`

#### Scenario: In the account
- **GIVEN** an approved builder
- **WHEN** the account screen opens
- **THEN** it shows the AI assistants part with the keys, the form and the ready prompt

#### Scenario: Not a builder
- **GIVEN** a user who is not an approved builder or an admin
- **WHEN** the account screen opens
- **THEN** there is no AI assistants part

#### Scenario: My bots
- **WHEN** an approved builder opens "My bots"
- **THEN** it shows the bots and one link "Access keys for AI assistants" to `/account#keys`, and no keys, form or prompt
