## ADDED Requirements

### Requirement: ADM-15 Public addresses and llms.txt
`Admin:SiteUrl` (start `https://dalanet.ir`) and `Admin:BotApiUrl` (start `https://api.dalanet.ir`) SHALL be admin settings. `GET /api/public/settings` SHALL also give them as `site_url` and `bot_api_url`. `GET /llms.txt` SHALL need no session and give a plain text page (`text/plain; charset=utf-8`) in English for AI agents that says how to connect a bot, how to get an access key, and how to use the MCP door and the CLI, with the two addresses from the settings at the time of the call.

#### Scenario: Read llms.txt
- **WHEN** a visitor with no session asks for `/llms.txt`
- **THEN** the answer is 200 `text/plain` and names `https://api.dalanet.ir`, `/api/mcp`, `dalan.mjs` and access keys

#### Scenario: Panel change
- **WHEN** an admin sets `Admin:BotApiUrl` to `https://bots.example.ir`
- **THEN** the next `/llms.txt` and `/api/public/settings` use `https://bots.example.ir`
