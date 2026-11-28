# m46: trust this device for the second factor

## Why
The owner (2026-11-28): after the password, every login asks for the authenticator code, even on the own phone.
Opening the app for a code each time is slow. Most sites have "Don't ask again on this device".

## What changes
- AUTH-44 (new): the code step has "Don't ask for the code on this device". A right code with it gives a random token
  in an HttpOnly, Secure cookie (`__Host-dalan_trusted`, SameSite Lax so it also comes back from GitHub); only its hash
  is kept, with the user, browser, system, address and an end time. Then a login of that user on that browser skips the
  code. Users see their trusted devices in "Login and security" and can forget one or all.
- Trust ends with the setting's days, when the second factor goes on or off, when the password is set, and on
  "Log out other devices" (all but this browser).
- SEC-15: an admin's login on a trusted device also skips the code. `Identity:TrustedDeviceDays` 0 turns trust off for all.
- CLI-92: the checkbox on the code step, and the list in the second-factor card.

New setting: `Identity:TrustedDeviceDays` (30, 0 to 365; 0 = off).
