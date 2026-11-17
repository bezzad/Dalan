# Tasks

## 1. Sessions (Identity)

- [ ] 1.1 Write tests for AUTH-08, AUTH-09, AUTH-10 and the changed AUTH-03, AUTH-05; add the `sessions` table, the refresh cookie, `POST /api/auth/refresh`; verify they pass

## 2. SMS login

- [ ] 2.1 Write tests for NTF-01, NTF-02; add `ISmsSender` and the two senders; verify they pass
- [ ] 2.2 Write tests for SEC-06; add the challenge and the proof check; verify they pass
- [ ] 2.3 Write tests for AUTH-11, SEC-07, SEC-08; add `POST /api/auth/sms` with its limits; verify they pass
- [ ] 2.4 Write tests for AUTH-12; add `POST /api/auth/sms/verify`; verify they pass

## 3. Password and username

- [ ] 3.1 Write tests for AUTH-13, AUTH-14, AUTH-15, SEC-09 and the changed AUTH-04; add the endpoints; verify they pass
- [ ] 3.2 Write tests for AUTH-16, AUTH-17; add the settings endpoint and the security log; verify they pass

## 4. Client

- [ ] 4.1 Write tests for CLI-10, CLI-11, CLI-12; build the pure modules; verify they pass
- [ ] 4.2 Build the login screens and the account screen; check the full flow in a real browser

## 5. Finish

- [ ] 5.1 Update `docs/ARCHITECTURE.md`, `docs/DESIGN.md`, `CLAUDE.md`, `README.md`, deploy notes
- [ ] 5.2 Verify CI is green, review the whole diff, archive this change
