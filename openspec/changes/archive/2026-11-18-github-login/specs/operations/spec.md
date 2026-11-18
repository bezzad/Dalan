# Spec Delta

## ADDED Requirements

### Requirement: OPS-14 GitHub login is set up by the install script
With `GITHUB_CLIENT_ID` and `GITHUB_CLIENT_SECRET` in its environment, `deploy/install.sh` SHALL pass them to `setup.sh` in a file of mode 600, never on a command line. `setup.sh` SHALL then set `Identity__GitHub__ClientId`, `Identity__GitHub__ClientSecret` and `Identity__GitHub__CallbackUrl` (`https://<site>/api/auth/github/callback`) in `dalan.env`, keep every other line, and restart `dalan` if it runs. Without them `dalan.env` SHALL NOT change.

#### Scenario: New values
- **GIVEN** a server set up before, with a `dalan.env` that has other secrets
- **WHEN** `setup.sh` runs with a GitHub file holding id `abc` and secret `s3`
- **THEN** `dalan.env` has `Identity__GitHub__ClientId=abc`, `Identity__GitHub__ClientSecret=s3` and the callback URL of the site, once each, and the other lines are the same

#### Scenario: No values
- **WHEN** `setup.sh` runs with no GitHub file
- **THEN** `dalan.env` does not change

#### Scenario: Not on the command line
- **WHEN** `install.sh` runs with `GITHUB_CLIENT_SECRET` set
- **THEN** no `ssh` or `scp` command line holds the secret, and the file sent holds it
