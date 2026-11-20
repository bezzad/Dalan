# Proposal

## Why

The deploy after PR 39 failed with "Connection closed by 80.244.7.112 port 22". The server log shows the GitHub runner connected and then sent nothing until the server's 120 s login limit ("Timeout before authentication"): the international link to Iran stalled. The server had no firewall, fail2ban or ban on the runner. The deploy opened a new SSH connection for each of its about ten steps, so one stalled handshake failed the whole deploy.

## What Changes

- `deploy/deploy.sh` opens one SSH connection (OpenSSH ControlMaster) with up to `SSH_TRIES` tries of `SSH_TIMEOUT` seconds, and every command and the rsync upload reuse it. Keepalives end a connection that stalls later.

## Capabilities

### Modified Capabilities

- `delivery`: new DEL-07.

## Impact

- `deploy/deploy.sh`, `tests/deploy/deploy_test.sh`, `docs/SERVER.md`.
- New deploy settings: `SSH_TRIES` (5), `SSH_TIMEOUT` (30), `SSH_WAIT` (5). No server or code change.
