# Spec Delta

## MODIFIED Requirements

### Requirement: SEC-07 SMS limits per phone number
The server SHALL send at most one SMS to a number in each `Identity:SmsWaitSeconds` (start 120). After `Identity:SmsPerPhonePerDay` (start 5) SMS to a number in any 24 hours, the wait SHALL be `Identity:SmsLongWaitSeconds` (start 1800) from the last SMS. A number in `Identity:AdminPhones` SHALL never get the long wait. A call too soon SHALL get 429 `sms_wait` with a `Retry-After` header that says how many seconds are left. A sent code's answer SHALL have `retry_in`: the seconds before the next SMS may go. One number's SMS SHALL NOT use up another number's limit.

#### Scenario: Second SMS too soon
- **GIVEN** an SMS went to a number 20 seconds ago and the wait is 60
- **WHEN** a client asks another one for the same number, with a valid proof
- **THEN** the response is 429 `sms_wait` with `Retry-After: 40` and no SMS is sent

#### Scenario: After the wait
- **GIVEN** an SMS went to a number 60 seconds ago and the wait is 60
- **WHEN** a client asks another one
- **THEN** the SMS is sent

#### Scenario: Daily limit
- **GIVEN** 5 SMS went to a number in the last 24 hours, the last one 2 minutes ago
- **WHEN** a client asks a sixth
- **THEN** the response is 429 `sms_wait` with `Retry-After: 1680`, and after that time the SMS is sent

#### Scenario: The fifth SMS says the long wait
- **WHEN** the fifth SMS in 24 hours is sent to a number
- **THEN** its answer has `retry_in` 1800

#### Scenario: A day later
- **GIVEN** a number that got 5 SMS, the last one more than 24 hours ago
- **WHEN** a client asks an SMS
- **THEN** the SMS is sent, and its `retry_in` is the short wait

#### Scenario: Admin number
- **GIVEN** the number is in `Identity:AdminPhones` and got 5 SMS in the last 24 hours
- **WHEN** a client asks a sixth after the short wait
- **THEN** the SMS is sent

#### Scenario: Another number
- **GIVEN** one number is waiting
- **WHEN** a client asks an SMS for another number
- **THEN** the SMS is sent

### Requirement: SEC-08 SMS limit per address and the daily budget
The server SHALL send at most `Identity:SmsPerIpPerDay` (start 200) SMS for calls from one client address in any 24 hours; over it the answer SHALL be 429 `sms_limit` with a `Retry-After` header: the seconds until the oldest of those SMS is 24 hours old. The server SHALL send at most `Identity:SmsPerDay` (start 1000) SMS in total in any 24 hours; over it the answer SHALL be 503 `sms_unavailable` and the server SHALL write an error log line, so the admin sees it.

#### Scenario: Address limit
- **GIVEN** the limit per address is 2 and one address got 2 SMS sent to two numbers, the first one an hour ago
- **WHEN** the same address asks an SMS for a third number
- **THEN** the response is 429 `sms_limit` with `Retry-After: 82800`

#### Scenario: Another address
- **GIVEN** one address is over its limit
- **WHEN** another address asks an SMS
- **THEN** the SMS is sent

#### Scenario: Budget is used up
- **GIVEN** the daily budget is 2 and 2 SMS were sent
- **WHEN** any client asks an SMS for a new number
- **THEN** the response is 503 `sms_unavailable`, no SMS is sent, and an error is logged

### Requirement: SEC-13 Email code limits
The server SHALL send at most one login email to an address in each `Identity:EmailWaitSeconds` (start 60). After `Identity:EmailPerAddressPerDay` (start 5) emails to an address in any 24 hours, the wait SHALL be `Identity:EmailLongWaitSeconds` (start 1800) from the last one. A call too soon SHALL get 429 `email_wait` with `Retry-After`. Over `Identity:EmailPerIpPerDay` (start 200) for one client address in any 24 hours the answer SHALL be 429 `email_limit` with `Retry-After`. The server SHALL send at most `Identity:EmailPerDay` (start 1000) in total, else 503 `email_unavailable`. Email counters are apart from SMS counters.

#### Scenario: Second email too soon
- **GIVEN** an email code went to an address 20 seconds ago
- **WHEN** a client asks another one for the same address
- **THEN** the response is 429 `email_wait` with `Retry-After: 40`

#### Scenario: Daily limit
- **GIVEN** 2 is the cap and 2 email codes went to an address, the last one 60 seconds ago
- **WHEN** a client asks a third
- **THEN** the response is 429 `email_wait` with `Retry-After: 1740`

#### Scenario: Budget is used up
- **GIVEN** the daily email budget is 1 and 1 email was sent
- **WHEN** a client asks a code for another address
- **THEN** the response is 503 `email_unavailable`, and an SMS can still be sent
