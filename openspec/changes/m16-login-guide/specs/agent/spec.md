## ADDED Requirements

### Requirement: AGT-16 Open the site in the browser
After `dalan http` is online the agent SHALL open the address of the first site in the default browser (`xdg-open`, `open`, or `cmd /c start`), once per run: a reconnect SHALL NOT open it again. It SHALL NOT open with `--no-open`, with `dalan start` or the service (AGT-14), with `dalan demo`, when output is not a terminal, on Linux without `DISPLAY` or `WAYLAND_DISPLAY`, or under systemd (`INVOCATION_ID`). A failure to open SHALL never stop the agent. `--no-open` SHALL be in the help.

#### Scenario: Desktop
- **GIVEN** `dalan http 3000 --name mysite` on a desktop in a terminal
- **WHEN** the site is online the first time
- **THEN** the address is opened once

#### Scenario: Reconnect
- **GIVEN** the address was opened
- **WHEN** the connection ends and comes back
- **THEN** it is not opened again

#### Scenario: Not a desktop
- **GIVEN** Linux with no `DISPLAY` and no `WAYLAND_DISPLAY`
- **WHEN** the site is online
- **THEN** nothing is opened

#### Scenario: Service and flag
- **WHEN** the agent runs as `start` or with `--no-open`
- **THEN** nothing is opened

#### Scenario: Open fails
- **GIVEN** the opening program is missing
- **WHEN** the site is online
- **THEN** the agent goes on and the site stays online
