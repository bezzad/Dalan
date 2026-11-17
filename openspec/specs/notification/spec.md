# notification Specification

## Purpose
How the server sends messages to people outside the app. For now: the login SMS, behind one small contract, with the provider chosen by a setting.

## Requirements

### Requirement: NTF-01 SMS goes through one small contract
Other services SHALL send an SMS only through the contract `ISmsSender` of `Dalan.Notification.Contracts`. The setting `Notification:SmsProvider` SHALL choose the sender: `kavenegar`, `log`, or empty. With an empty setting every send SHALL fail. The `log` sender writes the SMS to the log and sends nothing; with it outside the Development environment the server SHALL refuse to start. An unknown provider name SHALL also stop the start.

#### Scenario: No provider
- **GIVEN** `Notification:SmsProvider` is empty
- **WHEN** a login code is sent
- **THEN** the send fails

#### Scenario: Log sender in Development
- **GIVEN** `Notification:SmsProvider` is `log` in the Development environment
- **WHEN** a login code is sent
- **THEN** the send succeeds and the log has the code

#### Scenario: Log sender outside Development
- **WHEN** the server starts in the Production environment with `Notification:SmsProvider` set to `log`
- **THEN** it does not start, and the error says why

#### Scenario: Unknown provider
- **WHEN** the server starts with `Notification:SmsProvider` set to `pigeon`
- **THEN** it does not start, and the error says why

### Requirement: NTF-02 Kavenegar sender
The `kavenegar` sender SHALL send a login code with one HTTP call to `<Notification:Kavenegar:BaseUrl>/v1/<ApiKey>/verify/lookup.json` with `receptor` (the number as `09…`), `token` (the code) and `template` (`Notification:Kavenegar:Template`). An answer that is not a success, a network error, or no answer in `Notification:Kavenegar:TimeoutSeconds` (start 10) SHALL count as a failed send. The API key SHALL NOT be written to the log.

#### Scenario: Send
- **WHEN** the code `123456` is sent to `+989121234567`
- **THEN** the provider gets one call to `/v1/<key>/verify/lookup.json` with `receptor=09121234567`, `token=123456` and the template name

#### Scenario: Provider refuses
- **WHEN** the provider answers with an error status
- **THEN** the send fails

#### Scenario: Provider is down
- **WHEN** the call to the provider throws a network error
- **THEN** the send fails

#### Scenario: Key stays out of the log
- **WHEN** a send succeeds and another fails
- **THEN** no log line holds the API key
