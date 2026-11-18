# Spec Delta

## MODIFIED Requirements

### Requirement: NTF-01 SMS goes through one small contract
Other services SHALL send an SMS only through the contract `ISmsSender` of `Dalan.Notification.Contracts`. The setting `Notification:SmsProvider` SHALL choose the sender: `kavenegar`, `log`, or empty. With an empty setting every send SHALL fail. The `log` sender writes the SMS to the log and sends nothing. Outside the Development environment it SHALL send only to the numbers in `Notification:LogSmsPhones` (for tests on a server before a provider is ready), and with that list empty the server SHALL refuse to start. An unknown provider name SHALL also stop the start.

#### Scenario: No provider
- **GIVEN** `Notification:SmsProvider` is empty
- **WHEN** a login code is sent
- **THEN** the send fails

#### Scenario: Log sender in Development
- **GIVEN** `Notification:SmsProvider` is `log` in the Development environment
- **WHEN** a login code is sent
- **THEN** the send succeeds and the log has the code

#### Scenario: Log sender on a server
- **GIVEN** `Notification:SmsProvider` is `log` in the Production environment and `Notification:LogSmsPhones` lists `09121234567`
- **WHEN** a login code is sent to `+989121234567`, and another to a number not in the list
- **THEN** the first succeeds and the log has its code; the second fails and its code is not in the log

#### Scenario: Log sender outside Development
- **WHEN** the server starts in the Production environment with `Notification:SmsProvider` set to `log` and `Notification:LogSmsPhones` empty
- **THEN** it does not start, and the error says why

#### Scenario: Unknown provider
- **WHEN** the server starts with `Notification:SmsProvider` set to `pigeon`
- **THEN** it does not start, and the error says why
