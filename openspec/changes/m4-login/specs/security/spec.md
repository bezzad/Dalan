## ADDED Requirements

### Requirement: SEC-06 Proof of work before each SMS
`POST /api/auth/challenge` with `phone` SHALL return a `challenge` and a `difficulty` (`Identity:PowDifficultyBits`, start 14). An SMS SHALL be sent only with that challenge and a `nonce` such that the SHA-256 hash of the text `<challenge>.<nonce>` starts with at least `difficulty` zero bits. A challenge SHALL work only for its phone and for `Identity:PowChallengeSeconds` (start 120). Any other case SHALL get 400 `bad_proof` and send no SMS.

#### Scenario: Get a challenge
- **WHEN** a client posts a mobile number to `POST /api/auth/challenge`
- **THEN** the response is 200 with a `challenge` and `difficulty` 14

#### Scenario: Known hash
- **WHEN** the challenge `abc` and the nonce `0` are checked (SHA-256 of `abc.0` starts with the byte `20`, which is 2 zero bits)
- **THEN** the proof passes difficulty 2 and fails difficulty 3

#### Scenario: No work done
- **WHEN** a client asks an SMS with a real challenge and a nonce that does not give the zero bits
- **THEN** the response is 400 `bad_proof` and no SMS is sent

#### Scenario: Challenge of another phone
- **WHEN** a client solves the challenge of one phone and asks an SMS for another phone
- **THEN** the response is 400 `bad_proof`

#### Scenario: Old challenge
- **WHEN** a client sends a solved challenge after `Identity:PowChallengeSeconds`
- **THEN** the response is 400 `bad_proof`

#### Scenario: Made-up challenge
- **WHEN** a client sends a challenge the server did not make
- **THEN** the response is 400 `bad_proof`

### Requirement: SEC-07 SMS limits per phone number
The server SHALL send at most one SMS to a number in each `Identity:SmsWaitSeconds` (start 60) and at most `Identity:SmsPerPhonePerDay` (start 5) in any 24 hours. A call too soon SHALL get 429 `sms_wait` with a `Retry-After` header that says how many seconds are left. A call over the daily limit SHALL get 429 `sms_limit`. One number's SMS SHALL NOT use up another number's limit.

#### Scenario: Second SMS too soon
- **GIVEN** an SMS went to a number 20 seconds ago
- **WHEN** a client asks another one for the same number, with a valid proof
- **THEN** the response is 429 `sms_wait` with `Retry-After: 40` and no SMS is sent

#### Scenario: After the wait
- **GIVEN** an SMS went to a number 60 seconds ago
- **WHEN** a client asks another one
- **THEN** the SMS is sent

#### Scenario: Daily limit
- **GIVEN** 5 SMS went to a number in the last 24 hours
- **WHEN** a client asks a sixth
- **THEN** the response is 429 `sms_limit`

#### Scenario: A day later
- **GIVEN** a number that reached the daily limit
- **WHEN** 24 hours pass
- **THEN** an SMS is sent again

#### Scenario: Another number
- **GIVEN** one number is waiting
- **WHEN** a client asks an SMS for another number
- **THEN** the SMS is sent

### Requirement: SEC-08 SMS limit per address and the daily budget
The server SHALL send at most `Identity:SmsPerIpPerDay` (start 200) SMS for calls from one client address in any 24 hours; over it the answer SHALL be 429 `sms_limit`. The server SHALL send at most `Identity:SmsPerDay` (start 1000) SMS in total in any 24 hours; over it the answer SHALL be 503 `sms_unavailable` and the server SHALL write an error log line, so the admin sees it.

#### Scenario: Address limit
- **GIVEN** the limit per address is 2 and one address got 2 SMS sent to two numbers
- **WHEN** the same address asks an SMS for a third number
- **THEN** the response is 429 `sms_limit`

#### Scenario: Another address
- **GIVEN** one address is over its limit
- **WHEN** another address asks an SMS
- **THEN** the SMS is sent

#### Scenario: Budget is used up
- **GIVEN** the daily budget is 2 and 2 SMS were sent
- **WHEN** any client asks an SMS for a new number
- **THEN** the response is 503 `sms_unavailable`, no SMS is sent, and an error is logged

### Requirement: SEC-09 Password tries are limited
`POST /api/auth/password` SHALL serve at most `Identity:PasswordTries` (start 5) tries for one login name in each window of `Identity:PasswordTriesWindowMinutes` (start 15), without regard to letter case. A try over the limit SHALL get 429 `rate_limited` with a `Retry-After` header, even with the right password. One login name's tries SHALL NOT use up another's.

#### Scenario: Over the limit
- **GIVEN** the limit is 2 tries
- **WHEN** a client posts 2 wrong passwords and then the right one for the same login
- **THEN** the third answer is 429 `rate_limited` with `Retry-After`

#### Scenario: Another login
- **GIVEN** one login is over its limit
- **WHEN** another user logs in with the right password
- **THEN** the response is 200
