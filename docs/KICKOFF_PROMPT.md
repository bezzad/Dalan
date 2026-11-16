# Kickoff prompt for Claude Code

Paste the prompt below as the first message in a new Claude Code session opened on this repository. The design document it refers to is already here as `docs/DESIGN.md`.

```text
You are starting a new project called Dalan. Read docs/DESIGN.md fully before you do anything else. It is the source of truth for what we build and why.

WHAT DALAN IS
Dalan is a relay platform hosted in Iran. A developer runs a bot or web app on a home or office computer with no public IP. The home machine only makes outbound long-polling calls to Dalan. Users talk to Dalan. Dalan passes requests to the home machine and returns answers. Main site: dalanet.ir. Bot API host: api.dalanet.ir.

WHAT WE BUILD NOW
Only Layer 1 from the design document: private chat between a user and a bot (text, reply buttons, inline buttons, commands, pin, files), long polling and webhook for bots, a PWA client, SMS login, Web Push, an admin dashboard, and a Telegram adapter for these features. Do not build Layer 2 (site hosting, Agent, SDK, custom domains) or Layer 3 (payments). Do not add anything the design marks as out of scope: no user-to-user chat, no groups, no channels.

TECHNOLOGY (decided, do not change without asking)
- Server: C# on .NET 11, ASP.NET Core, Kestrel. One program, split into modules: Auth, Bot API (core), Telegram adapter, Client API, Push, Admin.
- Database: PostgreSQL. Message content in jsonb.
- Queues: System.Threading.Channels, one per bot, in memory, with expiry.
- Bot side: long polling (and webhook). Browser side: POST for commands, SSE for live messages.
- Client: a PWA in plain TypeScript with no framework. Persian (right to left) first, English second.
- Front proxy: Caddy.
- No Kafka, Redis or Orleans.
- Always use the latest stable version of every tool and package. Check the real current version; do not trust memory.

RULES
1. KISS, YAGNI and Clean Code. Write the smallest thing that works. No speculative abstractions or extra features.
2. The core never mentions Telegram. The Telegram adapter is a separate module that translates Telegram-shaped calls to the core.
3. Every limit (sizes, rates, expiry, history) and every domain name is configuration that the admin can change. Never hard-code them. Starting values are in section 18 of the design.
4. A bot never receives a user's phone number.
5. Production runs in Iran without Docker: one self-contained binary under systemd, deployed by CI over SSH. Docker is fine for local development only.
6. The running product must not load anything from a foreign host: no CDN fonts, scripts or analytics.
7. Security matters from day one: slow password hashing, hashed bot tokens, rate limits on every public endpoint, proof of work before each SMS, an audit log.
8. Write tests for the queue rules, auth rules and the Telegram adapter's request and response shapes.
9. Keep documentation short and current. Create CLAUDE.md with the project summary, commands, layout and these rules.

HOW TO WORK WITH ME
- I am a senior .NET developer, but I will mostly review, not type. Explain decisions briefly.
- I am not a native English speaker. Use simple, common words and short answers.
- Work step by step. Show me a short plan before each milestone and wait for my OK.
- If something is unclear or the design has a gap, ask me one question at a time. Do not guess on anything that is hard to undo.
- If you disagree with the design, say so with your reason before you build.

FIRST TASK
Do milestone 1 (Skeleton) from section 19 of the design:
1. Propose the repository layout (solution, projects, client folder, docs, deploy scripts) and wait for my OK.
2. Then create it: the solution, an empty module structure, a health endpoint, CI that builds and tests, the deploy script, CLAUDE.md and a README.
Stop after milestone 1 and summarize what exists and what comes next.
```

## Before you start

- [ ] Register `dalanet.ir` under your own IRNIC handle.
- [ ] Have the US test server ready with SSH access for CI.
