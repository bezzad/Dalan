## MODIFIED Requirements

### Requirement: SEC-05 Another site cannot act as the user
A request that another site makes in the user's browser SHALL NOT change anything. The session cookie is `SameSite=Strict` (AUTH-03), and every Client API and Identity call that changes data SHALL accept only a JSON body (`Content-Type: application/json`). The one exception, the file upload (REL-28), SHALL need the header `X-File-Name`, which a form of another site cannot send.

#### Scenario: Form post from another site
- **GIVEN** a logged-in user
- **WHEN** a message is posted as an HTML form (`application/x-www-form-urlencoded`)
- **THEN** the response is 400 `bad_request` and nothing is stored

#### Scenario: Upload without the header
- **GIVEN** a logged-in user
- **WHEN** a file is posted to the upload address as `multipart/form-data` without `X-File-Name`
- **THEN** the response is 400 `bad_request` and nothing is stored

## ADDED Requirements

### Requirement: SEC-12 A file cannot run in the site
Every file download (REL-29) SHALL carry its stored content type, `X-Content-Type-Options: nosniff` and `Content-Security-Policy: sandbox`. It SHALL carry `Content-Disposition: attachment` unless its type is `image/jpeg`, `image/png`, `image/gif`, `image/webp`, `audio/*` or `video/*`, which are shown inline.

#### Scenario: HTML file
- **GIVEN** a bot sent a file `page.html` of type `text/html`
- **WHEN** the user downloads it
- **THEN** the response has `Content-Disposition: attachment`, `nosniff` and a `sandbox` policy

#### Scenario: Photo
- **GIVEN** a bot sent a PNG photo
- **WHEN** the user downloads it
- **THEN** the response has type `image/png` and is not an attachment
