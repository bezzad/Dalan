# Spec Delta

## ADDED Requirements

### Requirement: DEL-07 The agent for every system ships with the server
Before each deploy, CI SHALL build the agent with Native AOT for linux-x64, osx-arm64, osx-x64 and win-x64 (each on its own system), and the deploy SHALL put the files in the release's `agent` folder, which is `Sites:AgentFolder` (AGT-10). Pull requests SHALL build at least the linux-x64 agent.

#### Scenario: Deploy
- **WHEN** a merge to main deploys
- **THEN** the new release has `agent/dalan-linux-x64`, `agent/dalan-macos-arm64`, `agent/dalan-macos-x64` and `agent/dalan-windows-x64.exe`
