# Proposal

## Why

The owner must be able to answer a legal complaint (cyber police, FATA): from which address did this user or this bot's owner work, and when. Today the client address is used only for the rate limit and is stored nowhere. The address also comes from `X-Forwarded-For` with the framework's default trust, written down in no rule: when a load balancer (for example Envoy) is put in front of several instances, every caller would silently get the balancer's address. This kind of thing is lost in later changes unless it is a rule with a test.

## What Changes

- **Real client address** (host): `X-Forwarded-For` is believed only when the call comes from a proxy listed in the new setting `Dalan:TrustedProxies`. The client's source port is read from the same header. The same address feeds the rate limit.
- **Warning for a forgotten proxy** (host): a call with `X-Forwarded-For` from a private address that is not in the list is logged as a warning.
- **Front proxy** (Caddy) sends the client's address and port and drops what the client wrote in the header.
- **Access log** (Admin, new schema `admin`): who (user or bot), address, port, exact UTC time. Written for every accepted Bot API and Client API call, at most one row per caller and address in a time span.
- **Retention**: rows older than a set number of days are deleted.

Out of scope: the admin screen that shows the log (milestone 8); addresses of calls with no known caller, for example an SMS code request (milestone 4 adds it on top of `IAccessLog`); the PROXY protocol.

## Capabilities

### New Capabilities

- `admin`: the access log.

### Modified Capabilities

- `security`: where the client address comes from; the per-address limit uses it.
- `operations`: the `admin` schema is applied at start.

## Impact

- New settings: `Dalan:TrustedProxies` (`127.0.0.1,::1`), `Admin:AccessLogIntervalMinutes` (60), `Admin:AccessLogRetentionDays` (180), `Admin:AccessLogSweepMinutes` (60).
- New database schema `admin` with table `access_log` (first Admin migration).
- New contract `Dalan.Admin.Contracts.IAccessLog`; Relay references `Dalan.Admin.Contracts`.
- `deploy/Caddyfile` changes; a load balancer added later must be listed in `Dalan:TrustedProxies`.
