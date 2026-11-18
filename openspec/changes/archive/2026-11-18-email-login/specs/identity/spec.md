# Spec Delta

## MODIFIED Requirements

### Requirement: AUTH-01 Identity owns the users
Identity SHALL keep users in the PostgreSQL schema `identity`. A user has a numeric id that never changes, a phone number or an email address or both, a display name and an optional username. A phone number SHALL belong to one user only, an email address SHALL belong to one user only (it is stored in lower case), and a username SHALL be unique without regard to letter case.

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

#### Scenario: Same username in another case
- **GIVEN** a user with username `Sara`
- **WHEN** a second user with username `sara` is saved
- **THEN** the save fails

### Requirement: AUTH-04 Who am I
`GET /api/me` SHALL return the `id`, `name`, `username`, `email`, `has_password` and `has_phone` of the logged-in user, and never the phone number or the password hash. Without a session it SHALL answer 401 `unauthorized`.

#### Scenario: Logged in
- **WHEN** a logged-in user calls `GET /api/me`
- **THEN** the response is 200 with the user's id and name, and the body does not contain the phone number

#### Scenario: Not logged in
- **WHEN** a call to `GET /api/me` has no session cookie
- **THEN** the response is 401 `unauthorized`

#### Scenario: Has a password
- **GIVEN** a user who set a password
- **WHEN** the user calls `GET /api/me`
- **THEN** `has_password` is true, and the body holds no hash

#### Scenario: Email user
- **GIVEN** a user who logged in by email code and has no phone
- **WHEN** the user calls `GET /api/me`
- **THEN** `email` is the address and `has_phone` is false

### Requirement: AUTH-13 Set a password
`POST /api/account/password` with `password` SHALL store an Argon2id hash of it (PHC text with its own salt and cost) and answer 204. A password shorter than `Identity:PasswordMinLength` (start 8) or longer than `Identity:PasswordMaxLength` (start 128) SHALL get 400 `weak_password`. When the user has a password and the session did not start with an SMS code or an email code, the call SHALL need the right `current_password`, else 403 `wrong_password`. A changed password SHALL end every other session of the user.

#### Scenario: First password
- **GIVEN** a logged-in user with no password
- **WHEN** the user posts a password of 8 characters
- **THEN** the response is 204 and `identity.users.password_hash` starts with `$argon2id$`

#### Scenario: Too short
- **WHEN** the user posts a password of 7 characters
- **THEN** the response is 400 `weak_password`

#### Scenario: Change needs the current password
- **GIVEN** a user logged in by password
- **WHEN** the user posts a new password without `current_password`, or with a wrong one
- **THEN** the response is 403 `wrong_password` and the old password still works

#### Scenario: Change with the current password
- **GIVEN** a user logged in by password
- **WHEN** the user posts a new password with the right `current_password`
- **THEN** the response is 204 and only the new password works

#### Scenario: Forgot the password
- **GIVEN** a user with a password, logged in by SMS code
- **WHEN** the user posts a new password without `current_password`
- **THEN** the response is 204

#### Scenario: Forgot the password, email user
- **GIVEN** a user with a password, logged in by email code
- **WHEN** the user posts a new password without `current_password`
- **THEN** the response is 204

#### Scenario: Other devices are logged out
- **GIVEN** a user logged in on two devices
- **WHEN** the first device changes the password
- **THEN** the first device stays logged in and a refresh on the second device is 401

#### Scenario: Not logged in
- **WHEN** the call has no session
- **THEN** the response is 401 `unauthorized`

### Requirement: AUTH-14 Log in with a password
`POST /api/auth/password` with `login` (the phone number, the email address or the username) and `password` SHALL log the user in (AUTH-03, AUTH-08) when the password is right. An unknown login, a user with no password and a wrong password SHALL all get the same answer: 401 `wrong_login`.

#### Scenario: By phone
- **GIVEN** a user with phone `+989121234567` and a password
- **WHEN** the client posts `{"login":"09121234567","password":"<right>"}`
- **THEN** the response is 200 with the user's profile and sets both cookies

#### Scenario: By email, any letter case
- **GIVEN** a user with email `sara@example.com` and a password
- **WHEN** the client posts the login `Sara@Example.com` with the right password
- **THEN** the response is 200

#### Scenario: By username, any letter case
- **GIVEN** a user with username `Sara_1` and a password
- **WHEN** the client posts the login `sara_1` with the right password
- **THEN** the response is 200

#### Scenario: Wrong password
- **WHEN** the client posts a wrong password
- **THEN** the response is 401 `wrong_login` and no cookie is set

#### Scenario: Unknown login and no password look the same
- **WHEN** the client posts an unknown login, or the login of a user who never set a password
- **THEN** the response is 401 `wrong_login`

### Requirement: AUTH-16 Login settings for the client
`GET /api/auth/settings` SHALL need no session and SHALL return `dev_login` (is the development login on), `email_login` (is an email provider set, NTF-10), `password_min_length` and `name_max_length`, so the client hard-codes none of them.

#### Scenario: Read
- **WHEN** a client with no session calls `GET /api/auth/settings`
- **THEN** the response is 200 with `dev_login`, `email_login`, `password_min_length` 8 and `name_max_length` 64

### Requirement: AUTH-17 Security events are logged
The server SHALL write a log line for each login, failed login, sent SMS or email code, refused SMS or email code, reuse of an old refresh token, password change and added phone, with the event name and the user id when it is known. No log line SHALL hold a login code, a password, a token, a whole phone number or a whole email address. The only exceptions are the `log` senders (NTF-01, NTF-10), which exist for tests.

#### Scenario: Login is logged
- **WHEN** a user logs in by SMS code
- **THEN** the log has a line with the event `login` and the user id

#### Scenario: Failed login is logged
- **WHEN** a client posts a wrong password
- **THEN** the log has a line with the event `login_failed`

#### Scenario: No secret in the log
- **WHEN** a user asks a code, logs in with it and sets a password
- **THEN** no log line holds the code, the password or the whole phone number

#### Scenario: No email in the log
- **WHEN** a user asks an email code and logs in with it, with a real email sender
- **THEN** no log line holds the code or the whole email address

### Requirement: AUTH-19 Builder request
`POST /api/account/builder` SHALL set the user's builder state to `requested`, unless it is already `approved` (then nothing changes); it answers the profile. A user with no phone (level 1) SHALL get 403 `phone_required` and nothing changes. `GET /api/me` SHALL say `builder`: `none`, `requested` or `approved`. An admin's approval SHALL set `approved`; a refusal SHALL set `none`, and the user may ask again. Admins SHALL count as builders.

#### Scenario: Ask
- **WHEN** a user asks to become a builder
- **THEN** `GET /api/me` says `builder` `requested`

#### Scenario: No phone
- **GIVEN** a user who logged in by email and has no phone
- **WHEN** the user asks to become a builder
- **THEN** the answer is 403 `phone_required` and `builder` stays `none`

#### Scenario: Approved
- **GIVEN** a user asked
- **WHEN** an admin approves
- **THEN** `GET /api/me` says `builder` `approved`, and the user can create a bot at once (REL-36)

#### Scenario: Refused
- **GIVEN** a user asked
- **WHEN** an admin refuses
- **THEN** `GET /api/me` says `builder` `none`

## ADDED Requirements

### Requirement: AUTH-24 Ask for an email code
`POST /api/auth/email` with `email`, `challenge` and `nonce` (SEC-06) SHALL make a random code of `Identity:EmailCodeDigits` digits (start 6), store only a keyed hash of it, send it through `IEmailSender` (NTF-10), and answer 200 with `expires_in`, `retry_in` and `code_digits`. An address not like `name@host.tld`, or over 254 characters, SHALL get 400 `bad_email`. A failed send SHALL get 503 `email_unavailable` and not count. A suspended user gets 403 `suspended`.

#### Scenario: Code is sent
- **WHEN** a client asks a code for `Sara@Example.com ` with a valid proof
- **THEN** one email with a 6-digit code goes to `sara@example.com` and the response is 200 with `expires_in` 600 and `retry_in` 60

#### Scenario: Code is not stored in clear
- **WHEN** a code was sent
- **THEN** no column of `identity.email_codes` holds the code

#### Scenario: Not an email
- **WHEN** a client asks a code for `sara.example.com`
- **THEN** the response is 400 `bad_email` and no email is sent

#### Scenario: Sender fails
- **GIVEN** the email sender fails
- **WHEN** a client asks a code
- **THEN** the response is 503 `email_unavailable`, and a new try right after it is not refused by the wait

### Requirement: AUTH-25 Log in with the email code
`POST /api/auth/email/verify` with `email` and `code` SHALL log the user of that address in under the rules of AUTH-12, with `Identity:EmailCodeSeconds` (start 600) and `Identity:EmailCodeTries` (start 5); any other case gets 400 `wrong_code`. An address with no user yet SHALL need a `name` (else 400 `name_required`, and the code stays valid); with it a new user with that email and no phone SHALL be made. A suspended user SHALL get 403 `suspended`.

#### Scenario: New address with a name
- **GIVEN** an address with no user, and the answer `name_required`
- **WHEN** the client posts the same code again with the name "Sara"
- **THEN** a user "Sara" with that email and no phone exists and the response sets both cookies

#### Scenario: Known user
- **GIVEN** a user with the address and a code that was just sent
- **WHEN** the client posts the address and the code
- **THEN** the response is 200 with the user's profile

#### Scenario: Wrong code
- **WHEN** the client posts a wrong code
- **THEN** the response is 400 `wrong_code` and no cookie is set

#### Scenario: Expired code
- **GIVEN** a code sent more than `Identity:EmailCodeSeconds` ago
- **WHEN** the client posts it
- **THEN** the response is 400 `wrong_code`

#### Scenario: Used once
- **GIVEN** a code that logged the user in
- **WHEN** the same code is posted again
- **THEN** the response is 400 `wrong_code`

### Requirement: AUTH-26 Add a phone number (level 2)
With a session, `POST /api/account/phone` with `phone` and `code` SHALL check the SMS code (the code is asked with `POST /api/auth/sms`, the same rules as AUTH-12) and, when it is right, save the phone on the user and answer 200 with the profile (`has_phone` true). A wrong code SHALL get 400 `wrong_code`. A phone of another user SHALL get 409 `phone_taken`. A user who has a phone SHALL get 409 `has_phone`. The call SHALL NOT accept an access key.

#### Scenario: Add a phone
- **GIVEN** an email user with no phone, and an SMS code sent to `09121234567`
- **WHEN** the user posts the number and the code
- **THEN** the answer is 200 with `has_phone` true, and the user can now log in by SMS code with that number

#### Scenario: Wrong code
- **WHEN** the user posts a wrong code
- **THEN** the answer is 400 `wrong_code` and the user still has no phone

#### Scenario: Phone of another user
- **GIVEN** another user has the number
- **WHEN** the user posts the number and the right code
- **THEN** the answer is 409 `phone_taken`

#### Scenario: Already has a phone
- **GIVEN** a user with a phone
- **WHEN** the user posts another number and its right code
- **THEN** the answer is 409 `has_phone`
