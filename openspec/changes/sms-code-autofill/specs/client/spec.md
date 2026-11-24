# Spec Delta

## ADDED Requirements

### Requirement: CLI-59 The phone reads the SMS code by itself
The SMS code field SHALL have `autocomplete="one-time-code"`. When the browser has WebOTP (`OTPCredential`), the SMS code screen SHALL ask for the code with `navigator.credentials.get({ otp: { transport: ["sms"] } })`, put the code in the field and send the form. The request SHALL be aborted when the form is sent or the screen is left. The login SMS SHALL end with the line `@<site domain> #<code>`.

#### Scenario: Android reads the code
- **GIVEN** a browser with WebOTP
- **WHEN** the SMS code screen opens and the browser gives the code `123456`
- **THEN** the field holds `123456` and the form is sent

#### Scenario: Browser without WebOTP
- **GIVEN** a browser without `OTPCredential`
- **WHEN** the SMS code screen opens
- **THEN** no credential request is made, and the user types the code

#### Scenario: Refused or left
- **WHEN** the browser refuses the request, or the user leaves the screen first
- **THEN** nothing is filled and no error is shown

#### Scenario: iOS
- **GIVEN** an iPhone and an SMS that ends with `@dalanet.ir #123456`
- **WHEN** the user taps the code field
- **THEN** iOS offers `123456` over the keyboard
