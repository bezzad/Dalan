# Spec Delta

## ADDED Requirements

### Requirement: AGT-11 Demo command
`dalan demo <port>` SHALL open a demo for `http://localhost:<port>` (SITE-18), print its address, when it ends, and the line "A demo is for tests: do not use real accounts or secrets in it", and carry its requests like `dalan http`. On a reconnect it SHALL keep the same demo. On Ctrl+C it SHALL end the demo (SITE-20) and exit; when the server says the demo has ended, it SHALL say so and exit.

#### Scenario: Start
- **WHEN** a builder runs `dalan demo 3000`
- **THEN** it prints `https://<demo host>/<id>/`, the end time and the warning, and a visitor of that address sees the app on port 3000

#### Scenario: Stop
- **WHEN** the builder presses Ctrl+C
- **THEN** the demo ends and its address answers 410
