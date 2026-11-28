## ADDED Requirements

### Requirement: CLI-99 The Backup page
The admin part of the dashboard SHALL have a "Backup" item at `/dashboard/admin/backup`: the list of backups (time, size, uploaded) with Download and Restore for each, "Back up now", "Upload a backup", and the last results. Restore SHALL ask the admin to type `RESTORE` and say that everyone, the admin too, must log in again after it.

#### Scenario: The menu
- **WHEN** an admin opens the dashboard
- **THEN** the admin group has "Backup" at `/dashboard/admin/backup`; for anyone else that address is "Not found"
