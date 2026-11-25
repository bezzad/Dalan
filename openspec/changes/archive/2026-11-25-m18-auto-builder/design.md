# Design

- **New state.** `BuilderState.Refused = 3` (`refused` in JSON). An admin refusal sets it; before, a refusal set `none`, which auto-approval could not tell from "never asked".
- **One helper.** `UserAdmin.ApproveByPhoneAsync(userId)`: with `Identity:AutoBuilder` on, one `UPDATE` sets `approved` where the user has a phone and the state is `none`. Called after an SMS login, an added phone, a merge and the development login. `requested` and `refused` are never touched: they wait for the admin.
- **Ask.** `POST /api/account/builder`: `none` → `approved` with the setting on, else `requested`; `refused` → `requested` (the admin decides); `requested` and `approved` stay.
- **Once.** A migration approves the open requests of users with a phone (`requested` → `approved`).
- **Tests.** The test server sets `Identity:AutoBuilder` to false, so the tests of the manual flow stay as they are; the AUTH-35 tests turn it on.
- **New setting:** `Identity:AutoBuilder` = true.
