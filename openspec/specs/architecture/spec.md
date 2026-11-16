# architecture Specification

## Purpose
Keeps the code clean as it grows: each service is independent, talks to others only through contracts, and the relay core never depends on Telegram or the web layer.

## Requirements

### Requirement: ARC-01 Services depend only on contracts
A service SHALL NOT reference another service's implementation. It MAY reference other services' `.Contracts` only.

#### Scenario: Service references another service
- **WHEN** a service project references another service's implementation project
- **THEN** the architecture tests fail

### Requirement: ARC-02 Contracts stay pure
A `.Contracts` project SHALL contain only interfaces and data types and SHALL NOT reference any service implementation, plugin or the host.

#### Scenario: Contracts reference an implementation
- **WHEN** a `.Contracts` project references an implementation, plugin or host project
- **THEN** the architecture tests fail

### Requirement: ARC-03 Relay core does not know Telegram
The Relay service and its contracts SHALL NOT reference the Telegram adapter, and SHALL NOT contain the word "Telegram" in any type or namespace name.

#### Scenario: Relay mentions Telegram
- **WHEN** a type in Relay or Relay contracts references the Telegram adapter or has "Telegram" in its name
- **THEN** the architecture tests fail

### Requirement: ARC-04 Plugins use contracts only
The Telegram adapter SHALL reference only `.Contracts` projects and the ASP.NET Core framework, not any service implementation.

#### Scenario: Adapter references a service implementation
- **WHEN** the Telegram adapter references a service implementation project
- **THEN** the architecture tests fail

### Requirement: ARC-05 Only the host wires everything
Only the host project SHALL reference service implementations and plugins.

#### Scenario: A non-host project wires services
- **WHEN** any project other than the host references two or more service implementations or a plugin
- **THEN** the architecture tests fail
