# Spec Delta

## ADDED Requirements

### Requirement: ARC-06 The agent stands alone
The agent project (`agent/Dalan.Agent`) SHALL reference no other project and no package: only the .NET base library. No server project SHALL reference it.

#### Scenario: Agent with a project reference
- **WHEN** the agent project references a Dalan project or a package
- **THEN** the architecture test fails
