# notification Specification

## Purpose
How the server sends messages to people outside the app. For now: the login SMS, behind one small contract, with the provider chosen by a setting.

## Requirements

### Requirement: NTF-01 SMS goes through one small contract
Other services SHALL send an SMS only through `ISmsSender` (`Dalan.Notification.Contracts`). `Notification:SmsProvider` SHALL choose the sender: `smsir`, `log`, or empty, which fails every send. `log` writes the SMS to the log and sends nothing; outside Development it SHALL send only to `Notification:LogSmsPhones` (tests on a server before a provider is ready), and with that list empty the server SHALL refuse to start. An unknown provider SHALL also stop the start.

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

### Requirement: NTF-02 SMS.ir sender
The `smsir` sender SHALL send a code with one `POST <Notification:SmsIr:BaseUrl>/v1/send/verify`: header `x-api-key`, body `{"mobile", "templateId": <Notification:SmsIr:TemplateId>, "parameters": [{"name": <Notification:SmsIr:CodeParameter>, "value": <code>}]}`. Only HTTP 200 with `status` 1 SHALL count as sent; anything else, or no answer in `Notification:SmsIr:TimeoutSeconds` (start 10), SHALL fail and log both statuses. The key SHALL NOT be logged.

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

### Requirement: NTF-03 Push subscriptions
Notification SHALL store push subscriptions in `notification.push_subscriptions` (user, endpoint, `p256dh`, `auth`, time). An endpoint SHALL be unique: saving it again moves it to the new user. A user SHALL have at most `Notification:Push:MaxSubscriptionsPerUser` (start 10); saving one more removes the oldest.

#### Scenario: Same browser, new user
- **GIVEN** user A's subscription with endpoint E
- **WHEN** user B subscribes with endpoint E
- **THEN** E belongs to user B only

#### Scenario: Too many
- **GIVEN** the maximum is 2 and a user has 2 subscriptions
- **WHEN** the user subscribes a third endpoint
- **THEN** the user has the newest 2

### Requirement: NTF-04 Only known push services
A subscription SHALL be refused unless its endpoint is an `https` address of at most `Notification:Push:MaxEndpointLength` (start 1000) characters on a host in `Notification:Push:AllowedHosts` (the host or a subdomain of it), and its keys are base64url with 65 bytes (`p256dh`) and 16 bytes (`auth`). So the server never sends a request to an address a user chose.

#### Scenario: Unknown host
- **WHEN** a user subscribes with the endpoint `https://evil.example/push/1`, or with `http://fcm.googleapis.com/x`
- **THEN** it is refused and nothing is stored

#### Scenario: Subdomain of a known host
- **GIVEN** `AllowedHosts` holds `push.apple.com`
- **WHEN** a user subscribes with `https://web.push.apple.com/abc`
- **THEN** it is stored

#### Scenario: Bad keys
- **WHEN** a user subscribes with an `auth` key of 5 bytes
- **THEN** it is refused

### Requirement: NTF-05 Push needs keys
Push SHALL be on only when `Notification:Push:PublicKey` and `Notification:Push:PrivateKey` (a VAPID key pair, base64url) are set. With both empty push is off: there is no public key, a subscribe is refused and nothing is sent. With a key that is not valid, or only one of the two, the server SHALL NOT start, and the error says why.

#### Scenario: Wrong key
- **WHEN** the server starts with `Notification:Push:PrivateKey` set and `PublicKey` empty
- **THEN** it does not start, and the error names `Notification:Push`

### Requirement: NTF-06 Sending a push
A push SHALL be sent to every subscription of its user with the Web Push standard: one POST to the endpoint, payload JSON `{"title","body","url","tag"}` encrypted with `aes128gcm` for that subscription, a VAPID `Authorization` header (subject `Notification:Push:Subject`), `TTL` from `Notification:Push:TtlSeconds` (start 86400), `Urgency: high`, and `Topic` set to the tag. The body SHALL be cut to `Notification:Push:MaxBodyLength` (start 200) characters.

#### Scenario: Two devices
- **GIVEN** a user with two subscriptions
- **WHEN** a push for that user is sent
- **THEN** each endpoint gets one POST with `Content-Encoding: aes128gcm`, an `Authorization` header starting `vapid `, `TTL`, `Urgency: high` and the `Topic`, and its body decrypts with that subscription's keys to the JSON with title, body, url and tag

#### Scenario: Long text
- **GIVEN** the body limit is 10
- **WHEN** a push with a body of 50 characters is sent
- **THEN** the decrypted body has at most 10 characters

#### Scenario: Nobody to send to
- **WHEN** a push is sent for a user with no subscription
- **THEN** no request is made

### Requirement: NTF-07 Push runs in the background
Sending a push SHALL NOT make its caller wait. A push waits in a queue of `Notification:Push:QueueSize` (start 10000); when the queue is full a new push is dropped with a log line. `Notification:Push:Workers` (start 4) send at once; a call that takes longer than `Notification:Push:TimeoutSeconds` (start 10) fails.

#### Scenario: Full queue
- **GIVEN** the queue size is 1 and the push service does not answer
- **WHEN** three pushes are sent at once
- **THEN** each send returns at once, and a log line says a push was dropped

### Requirement: NTF-08 Dead subscriptions are removed
When a push service answers 404 or 410, that subscription SHALL be deleted. Any other failure SHALL be logged and the subscription kept. A push is not sent again.

#### Scenario: Gone
- **WHEN** the push service answers 410 for a subscription
- **THEN** that subscription is deleted and the user's other subscriptions stay

#### Scenario: Push service has a problem
- **WHEN** the push service answers 500
- **THEN** the subscription stays and a log line says the push failed

### Requirement: NTF-09 Push secrets stay out of the log
No log line SHALL hold the VAPID private key, a subscription key, or the payload text.

#### Scenario: Send and fail
- **WHEN** one push succeeds and another fails
- **THEN** no log line holds the private key, the `auth` key, or the text of the push

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

### Requirement: NTF-12 Inbox
Every user SHALL have an inbox of notices (title, short text, an optional link on this site, time, read). Other services SHALL add one through `IInbox` (Notification.Contracts). `GET /api/inbox?before=<id>` SHALL list the user's own, newest first, `Notification:InboxPageSize` (start 30) a page, with `unread`; `POST /api/inbox/read` `{"up_to"}` SHALL mark them read. Notices older than `Notification:InboxDays` (start 90) SHALL be removed. Each call writes the access log.

#### Scenario: A notice
- **GIVEN** a notice for Sara
- **WHEN** Sara lists her inbox, then marks it read
- **THEN** the list has it with `read` false and `unread` 1, then `unread` 0

#### Scenario: Only one's own
- **WHEN** Ali lists his inbox
- **THEN** Sara's notices are not in it

### Requirement: NTF-13 A notice reaches the phone
Each new notice SHALL be pushed to the user's devices (NTF-06) with its title and link. When the user has no push subscription, or every push of it fails, the user's phone SHALL get an SMS: the title and the link, at most `Notification:InboxSmsLength` (start 120) characters, and at most `Notification:InboxSmsPerDay` (start 5) a day. A user with no phone gets no SMS. Each SMS is recorded (ADM-04), asked by 127.0.0.1.

#### Scenario: Push works
- **GIVEN** Sara has one push device that accepts
- **WHEN** a notice is added for her
- **THEN** it is pushed and no SMS is sent

#### Scenario: Push cannot reach
- **GIVEN** every push service refuses or times out
- **WHEN** a notice is added for Sara, who has a phone
- **THEN** one short SMS goes to her phone and is recorded

#### Scenario: Daily limit
- **GIVEN** Sara got `Notification:InboxSmsPerDay` SMS notices today
- **WHEN** another notice cannot be pushed
- **THEN** no SMS is sent; the notice is still in the inbox

#### Scenario: SMS.ir notice template
- **GIVEN** `Notification:SmsProvider` is `smsir`, `Notification:SmsIr:NoticeTemplateId` is `654321` and `Notification:SmsIr:NoticeParameter` is `TEXT`
- **WHEN** a notice SMS is sent
- **THEN** SMS.ir gets the verify call of NTF-02 with that template and the text in `TEXT`; with the template 0 no call is made and the send fails
