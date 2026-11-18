# Proposal

## Why

The owner asked to look at ngrok's Gateway page and use it to tell customers clearly what Dalan gives them. That page lists the building blocks of the product, grouped, so a visitor sees in one look what they get. Dalan's landing page shows two product cards but does not list what is inside them, what is ready and what is coming.

## What Changes

- A new "What you get" section on the landing page, right after the product cards (CLI-35). Four groups, each a card with an icon and a checklist:
  - **Bot relay** (ready): Telegram-compatible Bot API, outgoing-only long polling, buttons/commands/files, bot status and "not delivered", Web Push, showcase.
  - **Users and safety** (ready): verified users, no phone number for bots, rate limits and proof of work, hashed tokens and keys, access log.
  - **Tools for builders** (ready): CLI, MCP server, access keys, llms.txt.
  - **Sites and gateway** (all marked soon, Layer 2 backlog): local site at a fixed subdomain, own domain, preview links, webhook inspector and replay, webhook signature check, Basic Auth and IP allowlist, API keys and rate limits in front of an API, a local MCP server for AI agents.
- One honest line under it: what Dalan does not offer (raw TCP ports, SSH or remote desktop, AI gateway).

Inspired by the structure only: Dalan's own words, no ngrok text or logos. Persian first.

Out of scope: light theme and alignment (other work), login, pricing, customer logos.

## Capabilities

### New Capabilities

- `client`: CLI-35 (What you get).

## Impact

Client only. No new setting, no server change.
