## MODIFIED Requirements

### Requirement: AGT-14 Run as a service
`dalan service install` SHALL make the saved sites run at start-up: a systemd user unit on Linux and a launchd agent on macOS, both restarted after a crash; on Windows the user's "Run" key (no admin needed) with the agent's own path, `start --background` and its config, started at once and checked. It SHALL print where the log is and, on a failure, what failed and what to do. `uninstall` SHALL stop and remove it. Install SHALL refuse with a clear message when there is no key or no saved site.

#### Scenario: Linux
- **WHEN** `dalan service install` runs on Linux with a key and a saved site
- **THEN** a systemd user unit is written, enabled and started

#### Scenario: Windows
- **GIVEN** the agent at `C:\Users\Sara Ahmadi\AppData\Local\dalan\dalan.exe`
- **WHEN** `dalan service install` runs
- **THEN** the Run key's value is that path in quotes with `start --background` and the config path, the agent starts, and the output names the log file `%APPDATA%\dalan\dalan.log` and that it starts at log-on

#### Scenario: Windows failure
- **WHEN** `reg add` fails
- **THEN** the output says the service was not installed, gives reg's own message, and says how to run it by hand with `dalan start`

#### Scenario: Nothing saved
- **WHEN** a user with no saved site runs `dalan service install`
- **THEN** the agent stops with a message that says to run `dalan http <port> --name <name>` first

