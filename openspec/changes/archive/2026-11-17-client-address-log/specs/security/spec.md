## MODIFIED Requirements

### Requirement: SEC-02 Rate limit per IP
The server SHALL serve each client IP address at most `Dalan:IpRequestLimit` calls in each window of `Dalan:IpLimitWindowSeconds` (start: 100 calls per 1 second) on every endpoint of every service, counted before any token or session is checked. Static files of the client and the probes do not count. A call over the limit SHALL get HTTP 429 with error code `rate_limited` and a `Retry-After` header. The client address SHALL be the real client address of SEC-06.

#### Scenario: Over the limit
- **GIVEN** the limit is 2 calls per window
- **WHEN** one address makes 3 calls in the same window, with or without a token
- **THEN** the third gets 429 `rate_limited`

#### Scenario: Two addresses behind the proxy
- **GIVEN** one address is over its limit
- **WHEN** a call arrives through the proxy from another address
- **THEN** that call is served

#### Scenario: Client API and Identity are covered
- **GIVEN** the limit is 2 calls per window
- **WHEN** one address makes 3 calls to `/api/me`
- **THEN** the third gets 429 `rate_limited`

#### Scenario: A faked header does not open a new limit
- **GIVEN** the limit is 2 calls per window and a caller that is not a trusted proxy
- **WHEN** it makes 3 calls, each with a different `X-Forwarded-For`
- **THEN** the third gets 429 `rate_limited`

## ADDED Requirements

### Requirement: SEC-06 Real client address
The server SHALL take the client's address and source port from `X-Forwarded-For` only when the connection comes from an address in `Dalan:TrustedProxies` (addresses or CIDR ranges, comma separated; start: `127.0.0.1,::1`). It SHALL read the header from the right and stop at the first address that is not trusted; that is the client. Otherwise the client is the address of the connection itself. The rate limit and the access log SHALL use this one address.

#### Scenario: Through the trusted proxy
- **GIVEN** a trusted proxy
- **WHEN** it passes a call with `X-Forwarded-For: 203.0.113.7:51000`
- **THEN** the client address is `203.0.113.7` and the port is `51000`

#### Scenario: Faked header
- **GIVEN** a caller that is not a trusted proxy
- **WHEN** it sends `X-Forwarded-For: 203.0.113.7`
- **THEN** the client address is the caller's own address

#### Scenario: Load balancer in front
- **GIVEN** the load balancer `10.0.0.5` is in `Dalan:TrustedProxies`
- **WHEN** a call arrives from it with `X-Forwarded-For: 1.1.1.1, 198.51.100.9`, where the client wrote `1.1.1.1` and the balancer added `198.51.100.9`
- **THEN** the client address is `198.51.100.9`

#### Scenario: Two proxies in a row
- **GIVEN** both `10.0.0.5` and `10.0.0.6` are trusted
- **WHEN** a call arrives from `10.0.0.6` with `X-Forwarded-For: 198.51.100.9, 10.0.0.5`
- **THEN** the client address is `198.51.100.9`

#### Scenario: Front proxy
- **WHEN** a client sends `X-Forwarded-For: 9.9.9.9` to the front proxy
- **THEN** the server gets `X-Forwarded-For` with only the client's real `address:port`

### Requirement: SEC-07 Warning for a proxy that is not listed
When a call that carries `X-Forwarded-For` comes from a private or loopback address that is not in `Dalan:TrustedProxies`, the server SHALL log a warning that names the address and the setting, once for each such address. This is the sign of a proxy or load balancer that was added without the setting, which would make every client look like the proxy.

#### Scenario: Forgotten load balancer
- **GIVEN** `10.0.0.5` is not in `Dalan:TrustedProxies`
- **WHEN** two calls with `X-Forwarded-For` arrive from `10.0.0.5`
- **THEN** one warning names `10.0.0.5` and `Dalan:TrustedProxies`

#### Scenario: Public caller
- **WHEN** a call with `X-Forwarded-For` arrives from the public address `198.51.100.9`
- **THEN** no warning is logged
