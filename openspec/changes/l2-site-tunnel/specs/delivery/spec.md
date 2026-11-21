# Spec Delta

## ADDED Requirements

### Requirement: DEL-08 The agent for every system ships with the server
Every release SHALL have the agent, built with Native AOT, in its `agent` folder, which is `Sites:AgentFolder` (AGT-10): `dalan-linux-x64` from each build (the server's own build, `scripts/ci.sh` and CI), and `dalan-windows-x64.exe`, `dalan-macos-arm64` and `dalan-macos-x64` from a CI run started by hand (each on its own system; Native AOT does not cross systems). A build that cannot make a file SHALL keep the one of the running release (OPS-16).

#### Scenario: Deploy
- **WHEN** a manual CI run deploys
- **THEN** the new release has `agent/dalan-linux-x64`, `agent/dalan-windows-x64.exe`, `agent/dalan-macos-arm64` and `agent/dalan-macos-x64`

#### Scenario: The server's own build
- **WHEN** the server builds and deploys a new main
- **THEN** the new release has a new `agent/dalan-linux-x64` and the running release's Windows and macOS files
