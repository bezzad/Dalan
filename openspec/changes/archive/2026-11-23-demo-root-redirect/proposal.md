# Proposal

## Why

The owner (2026-11-23): `https://dalans.ir/` (the demo host) answers the 404 page "no demo at this address". A visitor who types the bare domain lands on a dead end.

## What Changes

- SITE-21 (new): on the demo host, `GET /` with no cookie of a known demo redirects (302) to `Admin:SiteUrl`. With the cookie of a demo, `/` still goes to that demo (SITE-19). Other paths without a demo keep the 404 page, so a wrong demo link stays visible. 302, not 301: browsers keep a 301 forever.

## Capabilities

### New Capabilities

- `sites`: SITE-21.

## Impact

`SiteProxy.HandleDemoAsync` only. No new setting: it uses `Admin:SiteUrl`; when that is empty, `/` keeps the 404 page.
