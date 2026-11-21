# Spec Delta

## ADDED Requirements

### Requirement: CLI-39 My apps
The user menu SHALL open "My apps" at `/apps`. For a builder it SHALL list their sites (name, address as a link, online or offline) with a delete that asks first, and show a step-by-step guide: 1) install the agent, 2) log in with a key made here by one button (the full `dalan login` command, shown once, with Copy), 3) run `dalan http <port> --name <name>`, with an example for two sites. Every action SHALL end in a toast. A user who is not a builder SHALL see how to become one instead.

#### Scenario: Guide commands
- **WHEN** the guide is drawn for the name `levelwise` and port `3000`
- **THEN** it shows `dalan http 3000 --name levelwise`

#### Scenario: Not a builder
- **WHEN** a user who is not a builder opens "My apps"
- **THEN** they see the way to become a builder, and no site list
