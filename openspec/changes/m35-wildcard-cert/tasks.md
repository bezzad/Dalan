# Tasks

Owner asked 2026-11-27 (internet shutdown); choice: Let's Encrypt with ArvanCloud DNS. Each task: failing tests with the rule ID first. CI on GitHub (rule 11).

- [ ] 1. `deploy/wildcard-cert.sh` (run and the deploy hook) and `dalan-cert.service`, `dalan-cert.timer`; tests with a fake lego and systemctl (OPS-18)
- [ ] 2. `install.sh` downloads lego with its checksum; `setup.sh` installs it, the root-only key file, the group Caddy reads with, the units (OPS-18)
- [ ] 3. Caddyfile imports the wildcard site block when it exists; `caddy_test.sh` checks both cases (OPS-18)
- [ ] 4. `docs/SERVER.md`, `docs/ARCHITECTURE.md` decision; CI green, final review, merge, archive
- [ ] 5. Owner: make an ArvanCloud API key (DNS administrator) and run install.sh once with `ARVAN_API_KEY`
