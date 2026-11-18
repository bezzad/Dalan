# Spec Delta

## MODIFIED Requirements

### Requirement: SEC-06 Proof of work before each SMS
`POST /api/auth/challenge` with `phone` or `email` SHALL return a `challenge` and a `difficulty` (`Identity:PowDifficultyBits`, start 14). An SMS or a login email SHALL be sent only with that challenge and a `nonce` such that the SHA-256 hash of the text `<challenge>.<nonce>` starts with at least `difficulty` zero bits. A challenge SHALL work only for its phone or email and for `Identity:PowChallengeSeconds` (start 120). Any other case SHALL get 400 `bad_proof` and send nothing.

#### Scenario: Get a challenge
- **WHEN** a client posts a mobile number to `POST /api/auth/challenge`
- **THEN** the response is 200 with a `challenge` and `difficulty` 14

#### Scenario: Challenge for an email
- **WHEN** a client posts an email to `POST /api/auth/challenge`
- **THEN** the response is 200 with a `challenge`, and the solved challenge sends a code to that email only

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

## ADDED Requirements

### Requirement: SEC-13 Email code limits
The server SHALL send at most one login email to an address in each `Identity:EmailWaitSeconds` (start 60), else 429 `email_wait` with `Retry-After`. It SHALL send at most `Identity:EmailPerAddressPerDay` (start 5) to one address and `Identity:EmailPerIpPerDay` (start 200) for one client address in any 24 hours, else 429 `email_limit`. It SHALL send at most `Identity:EmailPerDay` (start 1000) in total, else 503 `email_unavailable`. Email counters are apart from SMS counters.

#### Scenario: Second email too soon
- **GIVEN** an email code went to an address 20 seconds ago
- **WHEN** a client asks another one for the same address
- **THEN** the response is 429 `email_wait` with `Retry-After: 40`

#### Scenario: Daily limit
- **GIVEN** 5 email codes went to an address in the last 24 hours
- **WHEN** a client asks a sixth
- **THEN** the response is 429 `email_limit`

#### Scenario: Budget is used up
- **GIVEN** the daily email budget is 1 and 1 email was sent
- **WHEN** a client asks a code for another address
- **THEN** the response is 503 `email_unavailable`, and an SMS can still be sent
