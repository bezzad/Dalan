# Proposal

## Why

On the Iran server, GitHub login fails often with `github_unavailable`. The server reaches `api.github.com`, but a call to `github.com` (the token exchange) sometimes stalls until the timeout; the next call usually works.

## What Changes

- Each call to GitHub that gets no answer (no connection, or none in `Identity:GitHub:TimeoutSeconds`) is sent again, up to `Identity:GitHub:Tries` times in all (start 3). An answer from GitHub, even an error, is not sent again.

## Capabilities

### Modified Capabilities

- `identity`: AUTH-29.

## Impact

- `GitHubClient`, `IdentityOptions`, `appsettings.json`, tests in `tests/Dalan.Identity.Tests`.
- New setting: `Identity:GitHub:Tries` (3). No database change.
