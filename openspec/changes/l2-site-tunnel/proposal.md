# Proposal

## Why

Layer 2 starts (DESIGN section 11). A builder wants to show a web app that runs on a home computer (first: Levelwise) to everyone, with no public IP and no server. Today Dalan carries only bot messages.

## What Changes

- **New service `Sites`** (schema `sites`): a site is a name owned by an approved builder. Its address is `<name>.<Sites:Domain>`. The sites domain is a separate registered domain (decision 61) and a setting.
- **Tunnel doors** under `/api/tunnel`: the agent opens a session for one or more sites, then gets visitor requests over WebSocket, or by long polling when WebSocket does not work (decision 61), and sends the answers back.
- **Public routing**: a request whose host is a site address goes through the open session to the home computer and back. Clear answers when the site is off, slow, too big or too busy.
- **The Dalan Agent** (`agent/`, decision 60): C# with Native AOT, one file per system. `dalan login <key>` once, then `dalan http 3000 --name levelwise`. One agent carries many sites; each site has its own name and its own local port.
- **"My apps"** in the client (`/apps`): the user's sites with online status and delete, and a step-by-step guide.

Not in this change: inspector, Basic Auth, IP allow list, TCP, custom domains, static upload, "Login with Dalan", the signed user header, WebSocket from the visitor's page.

Second PR of this change: the agent files for Linux, Windows and macOS built in CI and served by Dalan, the one-line install, Caddy for the sites domain (certificate per name on first visit), and `docs/SERVER.md`.

## Capabilities

### New Capabilities

- `sites`: site names, tunnel sessions, public routing.
- `agent`: the program on the home computer.

### Modified Capabilities

- `architecture`: the agent stands alone.
- `client`: "My apps".

## Impact

- New projects `src/Sites/Dalan.Sites`, `agent/Dalan.Agent`, `tests/Dalan.Sites.Tests`.
- The host uses WebSockets and sends site hosts to Sites before anything else.
- New settings: listed in design.md.
