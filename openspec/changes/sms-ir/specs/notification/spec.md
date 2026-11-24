# Spec Delta

## RENAMED Requirements

- FROM: `### Requirement: NTF-02 Kavenegar sender`
- TO: `### Requirement: NTF-02 SMS.ir sender`

## MODIFIED Requirements

### Requirement: NTF-01 SMS goes through one small contract
Other services SHALL send an SMS only through the contract `ISmsSender` of `Dalan.Notification.Contracts`. The setting `Notification:SmsProvider` SHALL choose the sender: `smsir`, `log`, or empty. With an empty setting every send SHALL fail. The `log` sender writes the SMS to the log and sends nothing. Outside the Development environment it SHALL send only to the numbers in `Notification:LogSmsPhones` (for tests on a server before a provider is ready), and with that list empty the server SHALL refuse to start. An unknown provider name SHALL also stop the start.

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
- **WHEN** the server starts with `Notification:SmsProvider` set to `pigeon` or `kavenegar`
- **THEN** it does not start, and the error says why

### Requirement: NTF-02 SMS.ir sender
The `smsir` sender SHALL send a login code with one HTTP call `POST <Notification:SmsIr:BaseUrl>/v1/send/verify`, with the API key in the header `x-api-key` and the JSON body `{"mobile": "09…", "templateId": <Notification:SmsIr:TemplateId>, "parameters": [{"name": <Notification:SmsIr:CodeParameter>, "value": <code>}]}`. Only HTTP 200 with `status` 1 in the answer SHALL count as sent. Any other answer, a network error, or no answer in `Notification:SmsIr:TimeoutSeconds` (start 10) SHALL count as a failed send, and the log SHALL say the HTTP status and the SMS.ir `status` number. The API key SHALL NOT be written to the log.

#### Scenario: Send
- **GIVEN** `Notification:SmsIr:TemplateId` is `123456` and `Notification:SmsIr:CodeParameter` is `CODE`
- **WHEN** the code `654321` is sent to `+989121234567`
- **THEN** the provider gets one `POST /v1/send/verify` with the key in `x-api-key` and the body `{"mobile":"09121234567","templateId":123456,"parameters":[{"name":"CODE","value":"654321"}]}`, and the send succeeds

#### Scenario: Provider refuses
- **WHEN** the provider answers 400 with `{"status":113,"message":"…"}`
- **THEN** the send fails and the log has `400` and `113`

#### Scenario: Not a success inside a 200
- **WHEN** the provider answers 200 with a `status` other than 1, or with a body that is not JSON
- **THEN** the send fails

#### Scenario: Provider is down
- **WHEN** the call to the provider throws a network error
- **THEN** the send fails

#### Scenario: Key stays out of the log
- **WHEN** a send succeeds and another fails
- **THEN** the log does not contain the API key
