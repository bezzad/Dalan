# Tasks

## 1. Server

- [x] 1.1 Migrations: `identity.users.email`, nullable `phone`, `identity.email_codes`, `admin.access_log.email`
- [x] 1.2 Write tests for NTF-10, NTF-11; build `IEmailSender`, the SMTP and log senders; verify they pass
- [x] 1.3 Write tests for AUTH-24 to AUTH-26, SEC-13 and the changed AUTH and SEC-06 rules; build them; verify they pass
- [x] 1.4 Write tests for ADM-10, ADM-11, ADM-16; build them; verify they pass

## 2. Client

- [x] 2.1 Write tests for CLI-33, CLI-34; build the email login and the phone card; check them in a real browser

## 3. Finish

- [x] 3.1 Update DESIGN, ARCHITECTURE, SERVER.md and `deploy/setup.sh`
- [x] 3.2 Verify CI is green, review the whole diff, archive this change
