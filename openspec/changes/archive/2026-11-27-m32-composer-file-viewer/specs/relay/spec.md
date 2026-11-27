# Relay

## ADDED Requirements

### Requirement: REL-57 View link for an HTML file
`POST /api/bots/{bot id}/files/{file id}/view` SHALL answer `{"url", "expires_at"}` when the user may get the file (REL-29) and it is HTML. The url SHALL be `https://<view host>/v/<token>`; the view host is `Relay:FileViewHost`, else `view.<Sites:Domain>`. The token SHALL name only this user, bot and file and work for `Relay:FileViewMinutes` (start 10). Errors: 404 as REL-29, 400 `not_html`, 503 `viewer_off`. The settings (REL-18) SHALL say `file_view`. Each call SHALL be in the access log.

#### Scenario: Link for a report
- **GIVEN** a bot sent `report.html` to Sara and `Sites:Domain` is `dalans.ir`
- **WHEN** Sara asks for its view link
- **THEN** the url starts with `https://view.dalans.ir/v/`, `expires_at` is 10 minutes later, and her call is in the access log

#### Scenario: Not HTML
- **WHEN** Sara asks for the view link of a PDF
- **THEN** the answer is 400 `not_html`

#### Scenario: Not hers
- **WHEN** Ali asks for the view link of Sara's file
- **THEN** the answer is 404

#### Scenario: Viewer off
- **GIVEN** `Relay:FileViewHost` and `Sites:Domain` are empty
- **WHEN** Sara asks for a view link
- **THEN** the answer is 503 `viewer_off`

#### Scenario: The client knows
- **WHEN** the client loads its settings (REL-18)
- **THEN** `file_view` is true when a view host is set (`Relay:FileViewHost` or `Sites:Domain`), and false when both are empty, so the card shows only Download

#### Scenario: What is HTML
- **WHEN** a file has type `text/html`, or no clear type (`application/octet-stream`, empty) and a name ending `.html` or `.htm`
- **THEN** it gets a view link; any other file answers 400 `not_html`
