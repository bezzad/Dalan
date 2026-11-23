## ADDED Requirements

### Requirement: SITE-21 The demo host's root goes to Dalan
On the demo host, a request for `/` with no cookie of a known demo SHALL get a 302 redirect to `Admin:SiteUrl`. With the cookie of a known demo, `/` SHALL follow SITE-19 and SITE-20. Any other path without a demo SHALL keep the 404 page. An empty `Admin:SiteUrl` SHALL keep the 404 page for `/`.

#### Scenario: Bare domain
- **GIVEN** `Admin:SiteUrl` is `https://dalan.test`
- **WHEN** a visitor with no cookie, or with the cookie of no demo, opens `https://<demo host>/`
- **THEN** the answer is 302 to `https://dalan.test/`

#### Scenario: Root of a live demo
- **GIVEN** a visitor with the `dalan_demo` cookie of a live demo
- **WHEN** the page loads `https://<demo host>/`
- **THEN** the agent of that demo gets `/`

#### Scenario: Wrong demo link
- **WHEN** a visitor with no cookie opens `https://<demo host>/nosuchdemo/`
- **THEN** the answer is the 404 page
