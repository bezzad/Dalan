## ADDED Requirements

### Requirement: ADM-21 Backups for the admin
`GET /api/admin/backups` SHALL list the archives of `Admin:BackupFolder` and its `uploads`, newest first (name, size, time, uploaded), whether a backup or restore waits, and both status files. `POST /api/admin/backups` SHALL write the backup request (202). `GET /api/admin/backups/{name}` SHALL send that archive; any other name SHALL get 404. Each request and download SHALL be in the audit log.

#### Scenario: Make and download
- **WHEN** the admin posts a backup request, then downloads `dalan-2026-11-28-070000.tar.gz`
- **THEN** the request file exists, the download is the archive's bytes, and the audit log has `backup.start` and `backup.download`

#### Scenario: A path as a name
- **WHEN** the admin asks for `..%2Fdalan.env`
- **THEN** the answer is 404

### Requirement: ADM-22 Restore by the admin
`POST /api/admin/backups/upload` SHALL save a gzip body of at most `Admin:BackupMaxUploadMb` (start 2048) as `uploads/dalan-upload-<UTC date and time>.tar.gz`. `POST /api/admin/backups/{name}/restore` with `{"confirm": "RESTORE"}` SHALL write the restore request with the name (202). Any other confirm SHALL get 400, a body that is not gzip 400, a larger body 413. Each upload and restore SHALL be in the audit log.

#### Scenario: Restore an upload
- **WHEN** the admin uploads an archive and restores it with `RESTORE`
- **THEN** the upload is in the list as uploaded, the restore request holds its name, and the audit log has `backup.upload` and `backup.restore`

#### Scenario: No confirm
- **WHEN** the admin restores without `RESTORE`
- **THEN** the answer is 400 and there is no request file
