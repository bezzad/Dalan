# Spec Delta

## MODIFIED Requirements

### Requirement: AUTH-29 GitHub login failures
The callback SHALL delete the state cookie on every call. Each failure SHALL answer 302 to `/?login_error=<code>` and set no session: `github_state` (no state, or not equal to the cookie), `github_denied` (the person said no at GitHub), `github_no_email` (no primary verified email), `github_unavailable` (no answer after `Identity:GitHub:Tries` tries, start 3, of `Identity:GitHub:TimeoutSeconds` each, start 10, or the code was refused), `suspended`.

#### Scenario: Wrong state
- **WHEN** the browser comes back with a state that is not the cookie's, or with no cookie
- **THEN** the answer is 302 to `/?login_error=github_state`, GitHub is not called and no session cookie is set

#### Scenario: Person said no
- **WHEN** the browser comes back with `error=access_denied`
- **THEN** the answer is 302 to `/?login_error=github_denied`

#### Scenario: No verified email
- **GIVEN** a GitHub account whose emails are not verified
- **WHEN** it comes back
- **THEN** the answer is 302 to `/?login_error=github_no_email` and no user is made

#### Scenario: GitHub cannot be reached
- **GIVEN** GitHub does not answer (international links are cut)
- **WHEN** the browser comes back
- **THEN** the answer is 302 to `/?login_error=github_unavailable`

#### Scenario: Suspended
- **GIVEN** the user of GitHub account 42 is suspended
- **WHEN** it comes back
- **THEN** the answer is 302 to `/?login_error=suspended` and no session cookie is set

#### Scenario: The link stalls once
- **GIVEN** the first call to GitHub gets no answer and the next one does
- **WHEN** the browser comes back
- **THEN** the user is logged in

#### Scenario: GitHub answers with an error
- **GIVEN** GitHub refuses the code
- **WHEN** the browser comes back
- **THEN** GitHub is called once for the token and the answer is 302 to `/?login_error=github_unavailable`
