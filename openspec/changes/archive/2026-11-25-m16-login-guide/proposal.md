# Proposal

## Why

Owner feedback of 2026-11-25:

- The login screen has no logo, and the password form hides behind a small link.
- After login the person should return to the page they came from. GitHub login loses the address today.
- The guides send a new user to another page to make a key, and do not know what the user already did.
- The "My apps" example shows a personal project name to everyone.
- `dalan http` prints the address but the user must copy and open it.
- Dalan runs in Iran and must work without the international internet: the installs must be checked.

## What changes

- CLI-36 modified, CLI-62: logo above the login card; the password form is the first thing; the other ways come under it, each with its logo.
- CLI-63: the `next` path is kept across login, only internal paths (no open redirect); default "My bots".
- CLI-64 to CLI-66: both guides are steppers; steps already done (from the server) are green; the key step makes the key in a modal; a visitor of the site guide is asked to log in first.
- CLI-67: examples use `mysite` and `shop`; the default site name comes from the user.
- AGT-16: `dalan http` opens the site once in the default browser, only on a desktop, off with `--no-open`.
- DEL-09: install scripts and the builder CLI name no host except Dalan's own.

## Not in this change

- A verified phone makes a builder at once (no admin approval), and merging a phone account with an email account. Both need server work in Identity and Relay and an answer from the owner; they get their own change.
