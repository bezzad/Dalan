## ADDED Requirements

### Requirement: CLI-100 Thin scrollbars shown on hover
On a device with a mouse, every scrollbar SHALL be thin (6px where the browser allows it) and SHALL be clear until the mouse is over the part that scrolls, or the keyboard focus is inside it, like Telegram. A parent under the mouse SHALL NOT show the scrollbars of the parts inside it. A touch screen SHALL keep the system's own scrollbars.

#### Scenario: Mouse over the message list
- **GIVEN** a chat on a computer
- **WHEN** the mouse is over the message list
- **THEN** a thin scrollbar shows at its edge; when the mouse leaves, it is gone

#### Scenario: Phone
- **WHEN** a chat is open on a phone
- **THEN** the scrollbars are the system's own
