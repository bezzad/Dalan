## MODIFIED Requirements

### Requirement: CLI-36 One simple login card
The login screen SHALL be one card. It SHALL open with the password form (CLI-62). Under it SHALL come one equal button per other way: SMS, GitHub (`/api/auth/github`, when `github_login` is on) and email (when `email_login` is on). Without `dalan.returning` in local storage the order SHALL be SMS, GitHub, email; after any login the client SHALL set it, and the order SHALL be GitHub, email, SMS. SMS and email SHALL open their form with a "back" button. The development login SHALL be a small link. A `login_error` (AUTH-29) SHALL show and leave the address.

#### Scenario: New browser
- **GIVEN** `github_login` and `email_login` are true and local storage has no `dalan.returning`
- **WHEN** the login screen opens
- **THEN** the buttons under the password form are, in order, SMS, GitHub, email

#### Scenario: Returning browser
- **GIVEN** the person logged in before on this browser
- **WHEN** the login screen opens
- **THEN** the buttons under the password form are, in order, GitHub, email, SMS

#### Scenario: GitHub on
- **GIVEN** `github_login` is true
- **WHEN** the login screen opens
- **THEN** a "Log in with GitHub" button links to `/api/auth/github`

#### Scenario: GitHub off
- **GIVEN** `github_login` is false
- **WHEN** the login screen opens
- **THEN** there is no GitHub button

#### Scenario: Error from GitHub
- **WHEN** the page opens at `/?login_error=github_unavailable`
- **THEN** the login card shows "GitHub cannot be reached now. Log in by email or SMS." in the person's language and the address becomes `/`

#### Scenario: Texts
- **WHEN** the error is `github_state`, `github_denied`, `github_no_email`, `github_unavailable` or `suspended`
- **THEN** it has its own text, in Persian and in English

## ADDED Requirements

### Requirement: CLI-62 Logo and password first
The login screen SHALL show the Dalan gate (`client/src/logo.ts`) above the card. The password form (login name, password, "Log in") SHALL be open at once, with no button to press first. A line "or" SHALL separate it from the other ways (CLI-36), and each other way SHALL have its own logo: a phone for SMS, an envelope for email, the GitHub mark for GitHub. `docs/UI.md` SHALL say so.

#### Scenario: Password form is first
- **WHEN** the login screen opens
- **THEN** it has the gate above the card, the password form inside it, and the other ways under the form

#### Scenario: Each way has a logo
- **WHEN** the buttons of the other ways are drawn
- **THEN** each has an icon before its words

### Requirement: CLI-63 Back to where the person came from
After a login the client SHALL show the address the person came from. A `next` address SHALL come from `?next=` or from `sessionStorage` key `dalan.next`; the client SHALL keep the address there before it leaves for GitHub, and SHALL clear it after use. An address SHALL be accepted only when it is one string that starts with one `/`, has no `//` or `/\` start, no `\`, no control character, and is at most 200 characters; anything else (`https://evil.example`, `//evil.example`, `/\evil.example`, `javascript:x`) SHALL be ignored. With no accepted address, a login from `/` SHALL go to "My bots" (`/bots`), and a login from any other address SHALL stay on it.

#### Scenario: Internal path
- **GIVEN** `next` is `/guide/site`
- **WHEN** the login ends
- **THEN** the screen is `/guide/site`

#### Scenario: Foreign address
- **GIVEN** `next` is `//evil.example` or `https://evil.example` or `/\evil.example`
- **WHEN** the login ends
- **THEN** the address is ignored and the person goes to `/bots`

#### Scenario: GitHub round trip
- **GIVEN** the person pressed "Log in with GitHub" on `/guide/site`
- **WHEN** GitHub sends them back to `/`
- **THEN** after the login the screen is `/guide/site` and `dalan.next` is empty

#### Scenario: No address
- **GIVEN** the person logs in from `/` with no `next`
- **WHEN** the login ends
- **THEN** the screen is `/bots`

### Requirement: CLI-64 The guides are steppers
`/guide/bot` and `/guide/site` SHALL show their steps as a stepper: the step to do now is open with "Back" and "Next"; a step already done is green, says "Done" and is closed, with a button to open it. The state SHALL come from the server: logged in (`GET /api/me`), builder (`builder`), an access key (`GET /api/account/keys` not empty), a site online (`GET /api/sites`). The open step SHALL be the first step not done. A step the server cannot know SHALL be done only by "Next". A new and an old user SHALL see the same steps, in the same order.

#### Scenario: First not done
- **GIVEN** the user is logged in and a builder, has no key and no site online
- **WHEN** the site guide opens
- **THEN** step 1 is done, the install step is open, and the key step is not done

#### Scenario: Old user
- **GIVEN** the user has a key and a site that is online
- **WHEN** the site guide opens
- **THEN** the steps of login, key and try it are done and the open step is the last one

### Requirement: CLI-65 The key step opens a modal
The key step of the site guide SHALL say: "If this is the first time you hand us a site, first register site access with a key", and SHALL have a link that opens a modal on the same page. The modal SHALL make the key (`POST /api/account/keys`) and show the `dalan login` command once, with Copy; it SHALL NOT leave the page. When the user already has a key the step SHALL be green and the link SHALL say "Make another key".

#### Scenario: Make a key in the guide
- **WHEN** the user presses the link in the key step
- **THEN** a modal opens on the same page, the key is made, the command is shown once, and the page address does not change

### Requirement: CLI-66 A visitor logs in first
A visitor on `/guide/site` SHALL see no steps, only "Log in first, then the steps come" and a "Log in" button. After the login the same address SHALL show the steps (CLI-63).

#### Scenario: Visitor
- **GIVEN** nobody is logged in
- **WHEN** `/guide/site` opens
- **THEN** the page shows the log in line and button, and none of the steps

### Requirement: CLI-67 Neutral examples
No text, example, test or doc of the product SHALL name a personal or company project. Examples SHALL use `mysite` and `shop`. The default site name of the guide SHALL be made from the user's username, else from the user's name, as lower case letters and digits starting with a letter and at most 20 long, else `mysite`.

#### Scenario: Name from the user
- **GIVEN** the username is `Sara_Rad`
- **WHEN** the default site name is made
- **THEN** it is `sararad`

#### Scenario: No letters
- **GIVEN** the name is written in Persian only and there is no username
- **WHEN** the default site name is made
- **THEN** it is `mysite`

### Requirement: CLI-68 A new page starts at the top
When the person goes to another address of the site (a header or footer link, the menu, a button), the screen SHALL scroll to the top. A link with a `#part` SHALL scroll to that part instead. Back and Forward SHALL keep the browser's own scroll place. This holds on a phone too, where the header and footer links are far apart.

#### Scenario: Footer link from the bottom
- **GIVEN** the person scrolled to the bottom of the landing page
- **WHEN** they press "About us" in the footer
- **THEN** the About page opens at the top

#### Scenario: Link with a part
- **WHEN** the person presses a link to `/#plans`
- **THEN** the screen scrolls to the plans, not to the top

### Requirement: CLI-69 No zoom on a phone
The page SHALL have the viewport `width=device-width, initial-scale=1, maximum-scale=1`, and the style sheet SHALL set `touch-action: manipulation` and a font size of at least 16px on every field, so a pinch, a double tap or a focused field never zooms the page.

#### Scenario: Viewport
- **WHEN** the page head is read
- **THEN** the viewport says scale 1 and no larger scale

#### Scenario: Field size
- **WHEN** the style sheet is read
- **THEN** inputs, selects and text areas are at least 16px
