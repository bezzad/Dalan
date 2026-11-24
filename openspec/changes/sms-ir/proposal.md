# Proposal

## Why

The owner (2026-11-24) bought an SMS.ir plan. Kavenegar was never bought: its licence steps were too hard. Login by SMS needs a real provider.

## What Changes

- NTF-01: the providers are `smsir`, `log`, or empty. `kavenegar` is gone.
- NTF-02: the SMS.ir sender. One call to `POST <BaseUrl>/v1/send/verify` (fast send with an approved template), key in the `x-api-key` header.
- ADM-06: the secret in the scenario is the SMS.ir API key.
- Deploy: `SMS=smsir` and `SMSIR_KEY` in `install.sh` and `setup.sh` (OPS-10 in the open change `m11-iran-server`).
- No NuGet package: the official one (`IPE.SmsIR`) is a thin wrapper over the same one call, and it writes no log we control.

## Capabilities

### Modified Capabilities

- `notification`: NTF-01, NTF-02.
- `admin`: ADM-06.

## Impact

Notification (sender, options), host settings, admin hidden settings, deploy scripts, docs. New settings, all under `Notification:SmsIr`:

| Setting | Start | Why |
| --- | --- | --- |
| `BaseUrl` | `https://api.sms.ir` | SMS.ir REST address; hidden from the panel like the other provider addresses |
| `ApiKey` | empty | A secret: only in the server environment |
| `TemplateId` | `0` | The number of the approved template `dalan-otp`; SMS.ir gives it after approval, the admin sets it in the panel |
| `CodeParameter` | `CODE` | The name of the code variable in the template text (`#CODE#`) |
| `TimeoutSeconds` | `10` | Same as before |
