# agent Specification

## Purpose
Defines the site agent on the home computer: one native file per system, installed with one line from Dalan itself, that opens an outbound tunnel so a local web app is reachable with no public IP.

## Requirements

### Requirement: AGT-01 One file, no .NET
The agent SHALL be published with Native AOT as one executable per system that runs with no .NET installed. The build SHALL fail on any trim or AOT warning.

#### Scenario: Linux build
- **WHEN** CI publishes the agent for linux-x64
- **THEN** it is one file, there is no warning, and `dalan version` runs on a machine with no .NET

#### Scenario: macOS build
- **WHEN** CI publishes the agent for osx-arm64 and osx-x64, each on a Mac of that CPU
- **THEN** each is one file, there is no warning, and `dalan version` runs

### Requirement: AGT-02 Login once
`dalan login <key>` SHALL save the access key, and the server address when given, in the config file (`~/.config/dalan/dalan.json`, or `%APPDATA%\dalan\dalan.json` on Windows). On Linux and macOS the file SHALL be readable only by its owner. A key that does not start with `dalan_` SHALL be refused.

#### Scenario: Save the key
- **WHEN** a user runs `dalan login dalan_abc`
- **THEN** the config file holds the key, and on Linux its mode is 600

### Requirement: AGT-03 Sites from the command line
`dalan http <port> [--name <name>]` SHALL run one site; more `<port> [--name <name>]` pairs SHALL run more sites in the same agent. A port without a name SHALL take the name saved for that port in the config file, else a new random name; the agent SHALL save the port and name pairs it runs.

#### Scenario: Two sites
- **WHEN** a user runs `dalan http 3000 --name mysite 8080 --name shop`
- **THEN** one session serves `mysite` from port 3000 and `shop` from port 8080

#### Scenario: Same name next time
- **GIVEN** `dalan http 3000` once made the random name `k3x9q2`
- **WHEN** the user runs `dalan http 3000` again
- **THEN** the site is `k3x9q2` again

### Requirement: AGT-04 Sites from the config file
`dalan start` SHALL run every site saved in the config file.

#### Scenario: Start all
- **GIVEN** a config file with two sites
- **WHEN** the user runs `dalan start`
- **THEN** both sites run in one session

### Requirement: AGT-05 WebSocket first, then long polling
The agent SHALL try WebSocket first. When the WebSocket cannot be opened, it SHALL use long polling on the same session, and tell the user.

#### Scenario: WebSocket blocked
- **GIVEN** a network where WebSocket fails
- **WHEN** the agent starts
- **THEN** it serves visitor requests by long polling

### Requirement: AGT-06 Reconnect by itself
After a lost connection or `session_ended`, the agent SHALL open a new session after a wait that grows from 1 to 30 seconds, and start with WebSocket again. A refused key or name SHALL stop the agent with the server's message.

#### Scenario: Server restarts
- **GIVEN** a running agent
- **WHEN** the connection is lost
- **THEN** the agent opens a new session and the site works again

### Requirement: AGT-07 Forward to the local app
The agent SHALL send each request to `http://localhost:<port>` of its site with `Host: localhost:<port>` and the other headers, and return the status, headers and body. When the local app does not answer, the visitor SHALL get 502 with a page that names the port. The body SHALL go back in one message when it ends quickly, else in parts (AGT-12).

#### Scenario: Local app stopped
- **GIVEN** nothing listens on port 3000
- **WHEN** a visitor opens the site of port 3000
- **THEN** the visitor gets 502 and the page says port 3000 does not answer

#### Scenario: Small answer
- **WHEN** the app answers with a short body at once
- **THEN** the agent sends it in one message

### Requirement: AGT-08 Many requests at once
The agent SHALL handle requests in parallel, so a slow request does not hold the others.

#### Scenario: Slow and fast
- **GIVEN** a slow request to the local app is running
- **WHEN** a second request comes
- **THEN** the second answer arrives before the first

### Requirement: AGT-09 Server address is a hostname
The default server SHALL be `https://tunnel.dalanet.ir`. `--server <url>` or the environment `DALAN_SERVER` SHALL change it.

#### Scenario: Another server
- **WHEN** the agent runs with `DALAN_SERVER=http://localhost:5000`
- **THEN** it opens its session there

### Requirement: AGT-10 One-line install from Dalan
`GET /install.sh` (Linux and macOS) and `GET /install.ps1` (Windows) SHALL give a script that downloads the agent for the user's system from the same Dalan site (`Admin:SiteUrl`) and puts it on the user's PATH, with no root and no .NET. `GET /downloads/<file>` SHALL serve only the known agent files from `Sites:AgentFolder`; any other name, or a file that is not there, SHALL answer 404. `Sites:AgentFolder` SHALL NOT be in the admin panel.

#### Scenario: Install on Linux
- **WHEN** a user runs `curl -fsSL https://dalanet.ir/install.sh | sh`
- **THEN** the script downloads `https://dalanet.ir/downloads/dalan-linux-x64`, and `dalan version` works

#### Scenario: Install on a Mac
- **WHEN** a user runs the same command on a Mac with an Apple or an Intel CPU
- **THEN** the script downloads `dalan-macos-arm64` or `dalan-macos-x64`

#### Scenario: Another system
- **WHEN** a user runs the same command on a system with no agent file
- **THEN** the script says so and installs nothing

#### Scenario: Another file in the folder
- **GIVEN** a file `secret.txt` in the agent folder
- **WHEN** someone asks for `/downloads/secret.txt`
- **THEN** the answer is 404

### Requirement: AGT-11 Demo command
`dalan demo <port>` SHALL open a demo for `http://localhost:<port>` (SITE-18), print its address, when it ends, and the line "A demo is for tests: do not use real accounts or secrets in it", and carry its requests like `dalan http`. On a reconnect it SHALL keep the same demo. On Ctrl+C it SHALL end the demo (SITE-20) and exit; when the server says the demo has ended, it SHALL say so and exit.

#### Scenario: Start
- **WHEN** a builder runs `dalan demo 3000`
- **THEN** it prints `https://<demo host>/<id>/`, the end time and the warning, and a visitor of that address sees the app on port 3000

#### Scenario: Stop
- **WHEN** the builder presses Ctrl+C
- **THEN** the demo ends and its address answers 410

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

### Requirement: AGT-12 Stream the answer
The agent SHALL send a part when it has `stream_chunk_kb` bytes, when the answer ends, or when no new byte came for 20 ms. Over WebSocket it SHALL keep at most `stream_window` parts of one answer without `ack`. On `cancel`, or 410 for a part sent by long polling, it SHALL stop reading that local answer.

#### Scenario: Big file
- **WHEN** a visitor downloads a file larger than one part
- **THEN** the file comes in parts and arrives whole

#### Scenario: Cancel
- **WHEN** the server sends `cancel` for a running stream
- **THEN** the agent closes its connection to the local app for it

### Requirement: AGT-13 Stop ends the session
On Ctrl+C, or SIGTERM (a service stop or a shutdown), the agent SHALL end its session on the server (at most 5 seconds) before it exits.

#### Scenario: Ctrl+C
- **GIVEN** a running agent
- **WHEN** the user presses Ctrl+C
- **THEN** the server ends the session and visitors get the offline page at once

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

### Requirement: AGT-15 Status
`dalan status` SHALL print whether the service is installed and running, then each site of the key's builder with its address and online or offline.

#### Scenario: One site online
- **GIVEN** the site `mysite` online
- **WHEN** the user runs `dalan status`
- **THEN** the output has `mysite`, its address and `online`
