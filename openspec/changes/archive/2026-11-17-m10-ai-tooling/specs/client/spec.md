## ADDED Requirements

### Requirement: CLI-30 Access keys and ready prompt
"My bots" SHALL have a part for AI agents: the builder's keys (name, made, expires) with a delete button, a form to make a key that shows the new key once with a copy button, and a ready prompt to copy. The ready prompt SHALL name the site address, the Bot API address, `/llms.txt`, the MCP door and the CLI, with the addresses from the public settings (ADM-15), and SHALL never hold a key.

#### Scenario: Ready prompt
- **GIVEN** the public settings say site `https://dalanet.ir` and Bot API `https://api.dalanet.ir`
- **WHEN** the ready prompt is made in Persian and in English
- **THEN** it has `https://dalanet.ir/llms.txt`, `https://dalanet.ir/api/mcp`, `https://dalanet.ir/dalan.mjs` and `https://api.dalanet.ir`

### Requirement: CLI-31 Command line tool
`node dalan.mjs <command>` SHALL read `DALAN_URL` and `DALAN_KEY` and call the builder REST calls with the key: `bots`, `create <username> <name>`, `token <bot id>`, `status <bot id>`. Success: the JSON answer on stdout, exit 0. An error answer: `{"error", "message"}` on stderr, exit 1. No or a wrong command, or a missing variable: the usage, exit 2.

#### Scenario: Create
- **WHEN** `node dalan.mjs create weather_bot Weather` runs against a server
- **THEN** it sends `POST /api/builder/bots` with `Authorization: Bearer <DALAN_KEY>` and the JSON body, prints the answer and exits 0

#### Scenario: Error
- **WHEN** the server answers 409 `username_taken`
- **THEN** stderr has the error code and message and the exit code is 1

#### Scenario: Usage
- **WHEN** it runs with no command, or without `DALAN_KEY`
- **THEN** it prints the usage and exits 2

### Requirement: CLI-32 The site serves the CLI
The site SHALL serve `/dalan.mjs`, one Node file (Node 18 or newer) that needs no package.

#### Scenario: Served by the site
- **WHEN** a browser or `curl` asks for `/dalan.mjs`
- **THEN** the answer is 200 and is the same file as `client/public/dalan.mjs`
