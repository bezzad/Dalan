## ADDED Requirements

### Requirement: CLI-80 Line microphone on the voice button
The voice button of a bot chat (CLI-46) SHALL show Dalan's line `mic` icon (an inline SVG in `currentColor`: a capsule, an arc under it and a short stem), not an emoji. Its screen reader name SHALL stay "Record".

#### Scenario: Voice button
- **WHEN** a bot chat opens with an empty input
- **THEN** the voice button holds an `svg.icon` with the three `mic` paths, has no emoji text, and its `aria-label` is "Record"
