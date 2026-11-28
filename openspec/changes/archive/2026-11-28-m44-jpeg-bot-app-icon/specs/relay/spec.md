## MODIFIED Requirements

### Requirement: REL-54 Install one bot
`GET /@{username}/manifest.webmanifest` SHALL need no session and give the bot's manifest: `id`, `start_url`, `scope` `/@username`, `name` and `short_name` its name, `display` "standalone"; icons only the bot's (REL-51), real size read when saved, purpose "any", when square and 192 or more, else Dalan's. Chrome takes only PNG, SVG or WebP, so a JPEG icon SHALL be listed as `/api/bots/{id}/icon.svg`: an SVG of the same size that holds the JPEG as it is, `image/svg+xml`, with a policy that loads nothing else. `GET /@{username}/icon` SHALL give that icon, else Dalan's 192. Unknown, deleted or suspended: 404. The `/@{username}` page SHALL link these instead of the main ones.

#### Scenario: Manifest of a bot
- **GIVEN** bot "mofidbot" named "Mofid" with a 512 by 512 PNG icon
- **WHEN** a browser asks for `/@mofidbot/manifest.webmanifest`
- **THEN** it is JSON with `start_url` "/@mofidbot", `scope` "/@mofidbot", `name` "Mofid", and one icon: the bot's, `sizes` "512x512", `purpose` "any"; no Dalan icon

#### Scenario: Real size
- **GIVEN** the bot set a 640 by 640 JPEG with `setMyProfilePhoto`
- **WHEN** a browser asks for its manifest
- **THEN** the icon has `sizes` "640x640"

#### Scenario: JPEG icon
- **GIVEN** bot "mofidbot" with a 512 by 512 JPEG icon
- **WHEN** a browser asks for its manifest
- **THEN** its one icon is `/api/bots/{id}/icon.svg`, type `image/svg+xml`, `sizes` "512x512", and that address gives an SVG that holds the JPEG

#### Scenario: Icon not fit for an app
- **GIVEN** the bot has no icon, or a 100 by 100 one, or a 600 by 400 one
- **WHEN** a browser asks for its manifest and `/@mofidbot/icon`
- **THEN** the manifest has Dalan's icons, and the icon address gives Dalan's 192 icon

#### Scenario: The bot's page links it
- **WHEN** a browser opens `/@mofidbot`
- **THEN** the page links `/@mofidbot/manifest.webmanifest`, and its `rel="icon"` and `rel="apple-touch-icon"` links are `/@mofidbot/icon`

#### Scenario: No such bot
- **WHEN** a browser asks for the manifest or the icon of a bot that does not exist or is suspended
- **THEN** the response is 404
