## MODIFIED Requirements

### Requirement: REL-54 Install one bot
`GET /@{username}/manifest.webmanifest` SHALL need no session and return a web app manifest for that bot: `id`, `start_url` and `scope` all `/@username`, `name` and `short_name` the bot's name, `display` "standalone", and icons. When the bot has an icon fit for an app (REL-51; square and at least 192 by 192), the icons SHALL be only that icon, with its real size and purpose "any". Otherwise they SHALL be Dalan's icons. The server SHALL read an icon's width and height from its header when it is saved. `GET /@{username}/icon` SHALL need no session and return the bot's app icon, or Dalan's 192 icon when it has none that fits. An unknown, deleted or suspended bot SHALL get 404 on both. The page served for `/@{username}` SHALL link this manifest and this icon instead of the main ones.

#### Scenario: Manifest of a bot
- **GIVEN** bot "mofidbot" named "Mofid" with a 512 by 512 PNG icon
- **WHEN** a browser asks for `/@mofidbot/manifest.webmanifest`
- **THEN** it is JSON with `start_url` "/@mofidbot", `scope` "/@mofidbot", `name` "Mofid", and one icon: the bot's, `sizes` "512x512", `purpose` "any"; no Dalan icon

#### Scenario: Real size
- **GIVEN** the bot set a 640 by 640 JPEG with `setMyProfilePhoto`
- **WHEN** a browser asks for its manifest
- **THEN** the icon has `sizes` "640x640"

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
