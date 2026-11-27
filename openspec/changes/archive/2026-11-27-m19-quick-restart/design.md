# Design

## Evidence (server, 2026-11-25, UTC)

| Time | Source | Line |
|---|---|---|
| 07:42:21 | systemd | `Stopping dalan.service` |
| 07:42:21 | Dalan | `Application is shutting down...` (the last line) |
| 07:42:21 to 07:42:52 | Caddy | `dial tcp 127.0.0.1:5000: connect: connection refused` (`api.dalanet.ir`, `dalanet.ir`) |
| 07:42:51 | systemd | `Stopped`, `Started` |
| 08:18:40 to 08:19:11 | same again | second deploy |

30 seconds is `Dalan:ShutdownTimeoutSeconds`. Kestrel stops listening at once, then waits for open connections up to that timeout.

## Decisions

1. **Find the holder with a test first.** The test opens a live stream, a bot long poll and a site tunnel, then stops the host, and checks it is gone within `Dalan:StopGraceSeconds`. The tunnel is the first suspect: `ApplicationStopping` is used only in `EventStream.cs`, `BotGateway.cs` and `Probes.cs`.
2. **End, do not wait.** A long-lived handler links its token with `ApplicationStopping` and closes: the stream ends, a long poll answers its empty list, a tunnel closes with WebSocket status 1001 ("going away"), so the agent reconnects to the new process (the agent already retries with backoff).
3. **Keep the upper limit.** `Dalan:ShutdownTimeoutSeconds` (30) still lets a normal request finish (OPS-03 as today). `StopGraceSeconds` is only the target the test checks.
4. **No two-process deploy.** A few seconds of gap is fine for now; bots and the client already retry.
