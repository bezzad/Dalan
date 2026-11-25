# Relay

## ADDED Requirements

### Requirement: REL-57 View link for an HTML file
`POST /api/bots/{bot id}/files/{file id}/view` SHALL answer `{"url", "expires_at"}` when the user may get the file (REL-29) and it is HTML (type `text/html`, or no clear type and a name ending `.html` or `.htm`). The url SHALL be `https://<view host>/v/<token>`; the view host is `Relay:FileViewHost`, or `view.<Sites:Domain>` when that is empty. The token SHALL be valid for `Relay:FileViewMinutes` (start 10) and name only this user, bot and file. A file the user may not get SHALL answer 404 as REL-29; a file that is not HTML SHALL answer 400 `not_html`; with no view host the answer SHALL be 503 `viewer_off`. Each call SHALL be in the access log (ADM-01).

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
