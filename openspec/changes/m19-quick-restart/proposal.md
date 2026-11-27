# Proposal

## Why

On 2026-11-25 a bot (ArcaneBot's Dalan copy) lost calls to `api.dalanet.ir`. Two causes:

1. **Each deploy stops Dalan for 30 seconds.** The server log shows `Stopping dalan.service` at 07:42:21 UTC and `Stopped` at 07:42:51; the same at 08:18:40 to 08:19:11 (the two deploys of that day). In between, Caddy logs `dial tcp 127.0.0.1:5000: connect: connection refused` for `api.dalanet.ir` and `dalanet.ir`. Dalan logs only "Application is shutting down..." and then nothing: the process waits the whole `Dalan:ShutdownTimeoutSeconds` (30). The live stream and the bot long poll end on `ApplicationStopping`; the site tunnels (`TunnelHub`, `TunnelSession`) and the background workers do not seem to, so Kestrel waits for them. The server builds main by itself (OPS-16), so every merge is 30 seconds of lost calls for every user and bot.
2. **A self-signed certificate, not from us.** The bot also saw `CERTIFICATE_VERIFY_FAILED ... self-signed certificate` 12 times (05:00, 06:28, 07:19, 08:13 UTC). Caddy logged no TLS error at those times, the server always gives the Let's Encrypt certificate, DNS has only 80.244.7.112, and the bot uses no proxy. So something on the network path between the home computer and the server answered. Dalan cannot fix that; a bot must retry a failed call.

## What Changes

1. **OPS-03 (modified).** On a stop signal, every long-lived connection (live stream, bot long poll, site tunnel, agent long poll) and every background worker SHALL end at once, so the server exits within `Dalan:StopGraceSeconds` (new, start 5) when nothing else runs. `Dalan:ShutdownTimeoutSeconds` stays the upper limit for a normal request that is still running.
2. The deploy keeps its order (switch, restart, `/ready`, rollback). With a fast stop, the gap is the stop plus the start, a few seconds.

Out of scope: zero downtime with two processes (a second port and a Caddy switch); the network that sends a false certificate.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `operations`: OPS-03.

## Impact

- Sites: `TunnelHub`, `TunnelSession`, `TunnelEndpoints` end on `ApplicationStopping`.
- Relay, Notification, Admin: the `BackgroundService` workers stop on their token at once.
- Host: `Dalan:StopGraceSeconds` in `appsettings.json`; `tests/Dalan.Host.Tests/ShutdownTests.cs`.
- Docs: `docs/SERVER.md` (deploy gap), DESIGN section 18 (new setting).
- New setting: `Dalan:StopGraceSeconds` 5.
