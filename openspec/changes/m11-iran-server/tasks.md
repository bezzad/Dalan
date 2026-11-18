# Tasks

## 1. Scripts

- [x] 1.1 Write tests for OPS-09 and OPS-10; build `setup.sh`, `install.sh`, `caddy.service`; verify they pass
- [x] 1.2 Write tests for OPS-11; build `backup.sh` and its timer; verify they pass
- [x] 1.3 Write tests for OPS-12; build `drill.sh`; verify they pass
- [x] 1.5 Write tests for OPS-13; Postfix and OpenDKIM in `setup.sh`; verify they pass
- [ ] 1.4 Run the tests in CI; update docs and `docs/SERVER.md`

## 2. On the Iran server (owner)

- [ ] 2.1 Server, DNS inside Iran, `install.sh`, secrets, first deploy from CI
- [ ] 2.2 Drill on: SMS login, ArcaneBot chat and buttons from Iranian mobile data with no VPN
- [ ] 2.3 DNS records for mail (SPF, DKIM, DMARC) and reverse DNS; a login email reaches Gmail
- [ ] 2.4 Fix what the drill found, archive this change
