# security Specification

## Purpose
Limits that protect public endpoints from floods and abuse.

## Requirements

### Requirement: SEC-01 Rate limit per bot
The Bot API SHALL serve each bot at most `Relay:BotRequestLimit` calls in each window of `Relay:RateLimitWindowSeconds` (start: 30 calls per 1 second). A call over the limit SHALL get HTTP 429 with error code `rate_limited` and a `Retry-After` header. One bot's calls SHALL NOT use up another bot's limit, and calls with a wrong token SHALL NOT use up a bot's limit.

#### Scenario: Over the limit
- **GIVEN** the limit is 2 calls per window
- **WHEN** a bot makes 3 calls in the same window
- **THEN** the third gets 429 `rate_limited` with `Retry-After`

#### Scenario: Another bot
- **GIVEN** one bot is over its limit
- **WHEN** another bot makes a call
- **THEN** that call is served

#### Scenario: Wrong token
- **GIVEN** many calls with a bot's id and a wrong secret
- **WHEN** the real bot makes a call
- **THEN** that call is served

### Requirement: SEC-02 Rate limit per IP
The Bot API SHALL serve each client IP address at most `Relay:IpRequestLimit` calls in each window of `Relay:RateLimitWindowSeconds` (start: 100 calls per 1 second), counted before the token is checked. A call over the limit SHALL get HTTP 429 with error code `rate_limited`. The client address SHALL be the one the front proxy reports in `X-Forwarded-For`.

#### Scenario: Over the limit
- **GIVEN** the limit is 2 calls per window
- **WHEN** one address makes 3 calls in the same window, with or without a token
- **THEN** the third gets 429 `rate_limited`

#### Scenario: Two addresses behind the proxy
- **GIVEN** one address is over its limit
- **WHEN** a call arrives through the proxy from another address
- **THEN** that call is served
