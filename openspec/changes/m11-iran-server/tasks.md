# Tasks

## 1. Scripts

- [x] 1.1 Write tests for OPS-09 and OPS-10; build `setup.sh`, `install.sh`, `caddy.service`; verify they pass
- [x] 1.2 Write tests for OPS-11; build `backup.sh` and its timer; verify they pass
- [x] 1.3 Write tests for OPS-12; build `drill.sh`; verify they pass
- [x] 1.5 Write tests for OPS-13; Postfix and OpenDKIM in `setup.sh`; verify they pass
- [x] 1.4 Run the tests (`scripts/ci.sh` on the owner's computer, change `server-pull-deploy`); update docs and `docs/SERVER.md`

## 2. On the Iran server (owner)

- [x] 2.1 Server, DNS inside Iran, `install.sh` with `PULL_DEPLOY=1`, secrets, the GitHub deploy key; first deploy by the server's own build of main (OPS-16). Done 2026-11-21: 80.244.7.112, DNS at ArvanCloud, `SITES_DOMAIN=dalans.ir`, release `32c55fb` built by the server
- [ ] 2.2 Drill on: SMS login, ArcaneBot chat and buttons from Iranian mobile data with no VPN
- [ ] 2.3 DNS records for mail (SPF, DKIM, DMARC) and reverse DNS; a login email reaches Gmail. SPF, DKIM and DMARC are published (2026-11-21); open: reverse DNS of 80.244.7.112 (now `hosted-by.webdade.cloud`) to `dalanet.ir` in the provider panel, then a login email to Gmail
- [ ] 2.4 Fix what the drill found, archive this change
