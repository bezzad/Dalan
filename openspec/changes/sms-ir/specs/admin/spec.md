# Spec Delta

## MODIFIED Requirements

### Requirement: ADM-06 List the settings
`GET /api/admin/settings` SHALL list every editable setting with `key` (like `Relay:MaxTextLength`), `value`, `start` (from the file and environment), `changed`, `kind` (`number`, `switch`, `text`) and `restart` (read only at start). Editable SHALL be every leaf under `Admin`, `Identity`, `Relay` and `Notification`, plus `Dalan:IpRequestLimit` and `Dalan:IpLimitWindowSeconds`. Secrets and start-only safety settings SHALL NOT be listed (the list is in design.md, decision 4).

#### Scenario: Limits are listed
- **WHEN** an admin lists the settings
- **THEN** the list holds `Admin:AccessLogRetentionDays` with value `180`, kind `number`, and `Relay:BotOnlineSeconds`, `Relay:ClientStatusRefreshSeconds`, `Relay:LatestCommandWins` (kind `switch`) and `Notification:Push:AllowedHosts` (kind `text`)

#### Scenario: Secrets are never listed
- **GIVEN** `Notification:SmsIr:ApiKey` is set
- **WHEN** an admin lists the settings
- **THEN** no entry has the key, `Identity:SigningKey`, `Notification:Push:PrivateKey`, `Dalan:TrustedProxies` or `Identity:AdminPhones`, and no value in the answer is the API key

#### Scenario: Start-only setting
- **WHEN** an admin lists the settings
- **THEN** `Notification:Push:Workers` has `restart` true and `Relay:MaxTextLength` has `restart` false
