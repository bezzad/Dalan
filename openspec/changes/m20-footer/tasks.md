# Tasks

Each task: write the failing tests tagged with the rule IDs, then the code. CI runs on GitHub (rule 11).

- [x] 1. Deploy tests (`tests/deploy/pull_deploy_test.sh`, `deploy_test.sh`): `version.env` has `Dalan__Version` from the commit time (Tehran) and `Dalan__Commit`; the folder keeps the sha name (OPS-17)
- [x] 2. `pull-deploy.sh`, `deploy.sh`: make and write both values (OPS-17)
- [x] 3. Host test and `Probes.cs`: `/health` gives `version` and `commit`; `dev` and "" by default (OPS-17)
- [x] 4. Client test (pure function): the footer line in both languages, the tooltip, the line without a version (CLI-72)
- [x] 5. `public.ts`, `api.ts`, `i18n.ts`: the line under the columns (CLI-72)
- [x] 6. `app.css`: `footer.site { margin-block-start: auto }`; test that the rule is there (CLI-73)
- [x] 7. `docs/UI.md` footer; `docs/SERVER.md`: the live version is at `/health` and in the footer
- [x] 8. CI green on GitHub, final review, merge (#93; the version that counts up: #96)
- [ ] 9. After the server deploys it: the footer of dalanet.ir shows `v1.1.<n>` of the live release; archive
