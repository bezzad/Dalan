## ADDED Requirements

### Requirement: CLI-75 Connection ring
The client SHALL NOT show a bar or toast while its event stream (CLI-08) is down. Instead a thin ring SHALL turn around the bot's icon in the chat header, or around the Dalan logo on other screens, until the stream opens again. Hovering the icon SHALL show "Connected" or "Connecting…" in the page language, and a hidden `role="status"` text SHALL say the same to screen readers.

#### Scenario: Stream down in a chat
- **GIVEN** the chat of @mofidbot is open
- **WHEN** the event stream ends
- **THEN** a turning ring shows around the bot's icon, no bar shows at the top, and the page does not move

#### Scenario: Back
- **WHEN** the stream opens again
- **THEN** the ring goes away and the status text says "Connected"

#### Scenario: Other screens
- **WHEN** the stream is down on the chat list
- **THEN** the ring turns around the Dalan logo in the header

#### Scenario: Tooltip
- **WHEN** a mouse hovers the bot's icon in English, while connected and then while connecting
- **THEN** the tooltip is "Connected", then "Connecting…"

#### Scenario: Reduced motion
- **GIVEN** the device asks for reduced motion
- **WHEN** the stream is down
- **THEN** the ring is shown still and dashed
