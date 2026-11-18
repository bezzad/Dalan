# Spec Delta

## MODIFIED Requirements

### Requirement: AUTH-01 Identity owns the users
Identity SHALL keep users in the PostgreSQL schema `identity`. A user has a numeric id that never changes, a phone number or an email address or both, a display name, an optional username and an optional GitHub account id. A phone number SHALL belong to one user only, an email address SHALL belong to one user only (it is stored in lower case), a GitHub account id SHALL belong to one user only, and a username SHALL be unique without regard to letter case.

#### Scenario: Schema after start
- **WHEN** the server has started on an empty database
- **THEN** the table `identity.users` exists

#### Scenario: Same phone number twice
- **GIVEN** a user with phone number `+989121234567`
- **WHEN** a second user with the same number is saved
- **THEN** the save fails

#### Scenario: Same email twice
- **GIVEN** a user with email `sara@example.com`
- **WHEN** a second user with the same email is saved
- **THEN** the save fails

#### Scenario: Same GitHub account twice
- **GIVEN** a user with GitHub account id `42`
- **WHEN** a second user with the same GitHub account id is saved
- **THEN** the save fails

#### Scenario: Same username in another case
- **GIVEN** a user with username `Sara`
- **WHEN** a second user with username `sara` is saved
- **THEN** the save fails

### Requirement: AUTH-16 Login settings for the client
`GET /api/auth/settings` SHALL need no session and SHALL return `dev_login` (is the development login on), `email_login` (is an email provider set, NTF-10), `github_login` (are `Identity:GitHub:ClientId` and `Identity:GitHub:ClientSecret` set, AUTH-27), `password_min_length` and `name_max_length`, so the client hard-codes none of them.

#### Scenario: Read
- **WHEN** a client with no session calls `GET /api/auth/settings`
- **THEN** the response is 200 with `dev_login`, `email_login`, `github_login`, `password_min_length` 8 and `name_max_length` 64

#### Scenario: GitHub on
- **GIVEN** `Identity:GitHub:ClientId` and `Identity:GitHub:ClientSecret` are set
- **WHEN** a client calls `GET /api/auth/settings`
- **THEN** `github_login` is true; with either one empty it is false

## ADDED Requirements

### Requirement: AUTH-27 Start the GitHub login
When GitHub login is on (AUTH-16), `GET /api/auth/github` SHALL set the cookie `__Host-dalan_github` (`HttpOnly`, `Secure`, `SameSite=Lax`, `Path=/`, `Max-Age` `Identity:GitHub:StateSeconds`, start 600) to a new random state of 256 bits, and answer 302 to `<Identity:GitHub:WebUrl>/login/oauth/authorize` with `client_id`, `redirect_uri` (`Identity:GitHub:CallbackUrl`), `scope=user:email` and the same `state`. When GitHub login is off it SHALL answer 404.

#### Scenario: Go to GitHub
- **WHEN** a browser opens `/api/auth/github`
- **THEN** the answer is 302 to GitHub's authorize address with the client id, the callback URL, scope `user:email` and a state equal to the new cookie

#### Scenario: Off
- **GIVEN** no GitHub client id is set
- **WHEN** a browser opens `/api/auth/github`
- **THEN** the answer is 404

### Requirement: AUTH-28 Come back from GitHub
`GET /api/auth/github/callback` with `code` and the right `state` (AUTH-29) SHALL read the GitHub user and its primary verified email, and log in (AUTH-03) the user with that GitHub id; else the user with that email, saving the GitHub id on it; else a new user with that email, GitHub id and GitHub name (or login), and no phone. It SHALL record the caller in the access log (ADM-01) and answer 302 to `/`. The GitHub token SHALL NOT be stored.

#### Scenario: New user
- **GIVEN** a GitHub account 42 named "Sara Rad" with the primary verified email `Sara@Example.com`, and no Dalan user with it
- **WHEN** the browser comes back with a code and the right state
- **THEN** the answer is 302 to `/` with both session cookies, and a user "Sara Rad" with email `sara@example.com`, GitHub id 42 and no phone exists

#### Scenario: Same email as an email user
- **GIVEN** a user who logged in by email code with `sara@example.com`
- **WHEN** GitHub account 42 with that verified email comes back
- **THEN** that same user is logged in and now has GitHub id 42

#### Scenario: Known GitHub account
- **GIVEN** a user with GitHub id 42, and GitHub now says the email is `new@example.com`
- **WHEN** GitHub account 42 comes back
- **THEN** that same user is logged in and no new user is made

#### Scenario: Access log
- **WHEN** GitHub account 42 logs in from `203.0.113.7` port `51000`
- **THEN** the access log has a row for that user with `203.0.113.7` and `51000`

### Requirement: AUTH-29 GitHub login failures
The callback SHALL delete the state cookie on every call. Each failure SHALL answer 302 to `/?login_error=<code>` and set no session: `github_state` (no state, or not equal to the cookie), `github_denied` (the person said no at GitHub), `github_no_email` (no primary verified email), `github_unavailable` (no answer in `Identity:GitHub:TimeoutSeconds`, start 10, or the code was refused), `suspended`.

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
