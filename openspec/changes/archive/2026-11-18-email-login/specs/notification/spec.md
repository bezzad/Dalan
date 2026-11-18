# Spec Delta

## ADDED Requirements

### Requirement: NTF-10 Email goes through one small contract
Other services SHALL send an email only through `IEmailSender` of `Dalan.Notification.Contracts`. `Notification:EmailProvider` SHALL choose the sender: `smtp`, `log`, or empty (every send fails). The `log` sender writes the code to the log and sends nothing; outside Development it SHALL send only to the addresses in `Notification:LogEmailAddresses` (any letter case), and with that list empty the server SHALL refuse to start. An unknown provider SHALL stop the start.

#### Scenario: No provider
- **GIVEN** `Notification:EmailProvider` is empty
- **WHEN** a login code is sent by email
- **THEN** the send fails

#### Scenario: Log sender on a server
- **GIVEN** `Notification:EmailProvider` is `log` in the Production environment and `Notification:LogEmailAddresses` lists `Sara@example.com`
- **WHEN** a login code is sent to `sara@example.com`, and another to an address not in the list
- **THEN** the first succeeds and the log has its code; the second fails and its code is not in the log

#### Scenario: Log sender outside Development without a list
- **WHEN** the server starts in the Production environment with `Notification:EmailProvider` set to `log` and `Notification:LogEmailAddresses` empty
- **THEN** it does not start, and the error says why

#### Scenario: Unknown provider
- **WHEN** the server starts with `Notification:EmailProvider` set to `pigeon`
- **THEN** it does not start, and the error says why

### Requirement: NTF-11 SMTP sender
The `smtp` sender SHALL send a plain text email (Persian and English, code in subject and body) from `Notification:Smtp:From` through `Notification:Smtp:Host`:`Port` (start 587), with STARTTLS when `Notification:Smtp:StartTls` is on (start on), and the settings' user name and password. An error or no answer in `Notification:Smtp:TimeoutSeconds` (start 10) is a failed send. No host or sender address SHALL stop the start. The password SHALL NOT be logged.

#### Scenario: Sent
- **GIVEN** an SMTP server that accepts mail
- **WHEN** a login code is sent to `sara@example.com`
- **THEN** the SMTP server gets one mail to that address that holds the code

#### Scenario: Server down
- **GIVEN** no SMTP server answers at the host
- **WHEN** a login code is sent
- **THEN** the send fails

#### Scenario: Missing host
- **WHEN** the server starts with `Notification:EmailProvider` `smtp` and no `Notification:Smtp:Host`
- **THEN** it does not start, and the error says why
