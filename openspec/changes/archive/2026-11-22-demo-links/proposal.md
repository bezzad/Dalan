# Proposal

## Why

The owner wants to show a local app to someone for a short time, like a Google Meet link: one command, one random address, gone after a day. Today a builder must claim a site name (`name.dalans.ir`), which stays forever. A demo is for tests of new features: nothing to claim, nothing to clean up.

## What Changes

- **Demo link** (SITE-18): `dalan demo <port>` gets a random address `https://dalans.ir/<id>/` (on `Sites:DemoHost`), which works for `Sites:DemoHours` (start 24). No site name, no bot token, no change in the app.
- **Path and cookie routing** (SITE-19): `dalans.ir/<id>/x` goes to the demo as `/x`. The first visit sets a cookie with the demo id, so the app's own links to `/app.js` from that browser go to the same demo. Most apps work unchanged; a browser shows one demo at a time (the last one opened).
- **End** (SITE-20): after the time, or when the owner stops `dalan demo`, the address shows "this demo has ended". Demos per builder are limited (`Sites:MaxDemosPerBuilder`, start 3).
- **Certificate** (SITE-17): the demo host gets a certificate like a claimed site.
- **Agent** (AGT-11): the command `dalan demo <port>`.

Who: a builder with an access key, like a site (the access log names the owner, rule 13). Anonymous links would be used for phishing.

Out of scope: a demo list in "My apps", a password on a demo, demos on a subdomain (a certificate per random name would hit the Let's Encrypt limit, about 50 names a week).
