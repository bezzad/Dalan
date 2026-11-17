# Design

## Context

Milestone 3 is merged. The host already calls `UseForwardedHeaders` (framework default: trust loopback, one hop) for the rate limit (SEC-02). Nothing stores an address. A milestone 4 thread is changing Identity now, so this change does not touch Identity.

## Goals / Non-Goals

**Goals:**
- For any user or bot, the admin can later list address, port and time.
- A proxy or load balancer in front cannot silently hide the client address, and a client cannot fake its address.
- A new endpoint is covered without anyone remembering it.

**Non-Goals:**
- No admin screen or read API (milestone 8). The table and its index are ready for it.
- No row for calls without a known caller (wrong token, not logged in, SMS request). Milestone 4 records the SMS request with the phone number through the same contract.
- No row per request. That is the access log of the front proxy, not ours.
- No PROXY protocol. It does the same job at TCP level but every hop must speak it; the header with a trusted list is enough and is what Caddy and Envoy do by default.

## Decisions

1. **`X-Forwarded-For` with a trusted list, not blind trust.** Anyone can write the header. The host uses the framework's `ForwardedHeaders` middleware and fills `KnownIPNetworks` from `Dalan:TrustedProxies` (addresses or CIDR ranges, comma separated; one string so one environment variable can replace it). `ForwardLimit` is off: the middleware walks the header from the right and stops at the first address that is not trusted, so one or many proxies work the same. Hand-written header parsing was rejected: the middleware is the reviewed path.
2. **The setting is read at start.** A new proxy is a deploy event anyway.
3. **Port.** Iranian mobile carriers put many subscribers behind one address (CGNAT); the carrier needs address + port + time to find the person. The middleware already reads `ip:port` from the header, so the front proxy sends it that way (`header_up X-Forwarded-For {remote}` in Caddy; for Envoy later `%DOWNSTREAM_REMOTE_ADDRESS%`). Port is empty when the proxy did not send one.
4. **The front proxy replaces the header.** Caddy is the first hop from the internet, so what the client wrote is dropped. If a load balancer is put in front of Caddy later, Caddy must list it as `trusted_proxies` and the host must list both.
5. **Warning for a forgotten proxy (SEC-07).** The real danger is a new balancer that nobody added to the list: the server would record the balancer's address for everyone, and nothing would fail. Sign of it: a call that carries `X-Forwarded-For` from a private or loopback address that is not trusted. The host logs a warning, once per such address. Calls from public addresses never warn (anyone may send the header), so the set of remembered addresses stays small.
6. **The access log lives in Admin** (it owns the audit log, `docs/ARCHITECTURE.md` section 2): schema `admin`, table `access_log` (id, actor, actor_id, address `inet`, port, at). Index on (actor, actor_id, at) for "addresses of this user", and on (at) for the retention delete.
7. **Written at the two doors, in the filters.** `BotApiFilter` records after the token check; `ClientApiFilter` records the session's user. Both filters sit on the route group, so every present and future endpoint of the group is covered. Relay calls the contract `IAccessLog` (sync, in Admin.Contracts); it knows nothing about the table.
8. **One row per caller and address per interval**, not per request. A bot polls every few seconds and a row for each poll says nothing new. The service keeps "last written" per (actor, id, address) in memory and inserts only when the interval (`Admin:AccessLogIntervalMinutes`) has passed. A new address is written at once. With several instances each one keeps its own memory, so there can be more rows, never fewer. The sweep drops old memory entries.
9. **A failed insert fails the call.** A call is not served without its record; the memory entry is removed so the next call tries again.
10. **Retention** `Admin:AccessLogRetentionDays`, start 180. This number is our reading of the six months that Iran's Computer Crimes Law asks access providers to keep traffic data; it is assumed, for the lawyer review (`docs/DESIGN.md` section 18). A background sweep deletes older rows.
11. **Stored address is normalized:** an IPv4 address that arrives as IPv6-mapped (`::ffff:1.2.3.4`) is stored as IPv4.
12. **A call with no network address is not recorded.** That happens only in the in-process test server.
