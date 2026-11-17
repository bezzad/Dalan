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
