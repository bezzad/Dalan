# Proposal

## Why

1. The owner cannot see which build is live. The server builds main by itself (OPS-16), and only `/health` tells the commit (`version`: the first 12 characters of the sha). ArcaneBot shows a version made from the time of the last code commit; Dalan should do the same (owner, 2026-11-25).
2. On a page with little content the footer comes up to the middle of the screen (owner, 2026-11-25). `main.public > .scroller` already fills the screen, but the footer sits right after the body inside it, and nothing pushes it down.

## What Changes

1. **Version from the commit (OPS-17).** A release's `version.env` SHALL hold `Dalan__Version=v<yy.mm.dd.HHMMSS>` (the commit time of main, Tehran time, like ArcaneBot) and `Dalan__Commit=<12 characters of the sha>`. `/health` gives both (`version`, `commit`). A local run stays `dev`.
2. **Copyright and version line (CLI-72).** Under the footer columns, one small grey line, the usual form: `© 2026 Dalan · v26.11.25.114726`. In Persian: `© ۲۰۲۶ دالان · v26.11.25.114726`. The year is the year of the version, so it moves by itself. The version keeps Latin digits and reads left to right in both languages; its tooltip is the commit. The client reads it from `/health` once per page load.
3. **Footer at the bottom (CLI-73).** On a page shorter than the screen the footer sits at the bottom of the screen; on a longer page it comes after the content. It never covers content and is not fixed while scrolling. Every size (320px phone to wide desktop, portrait and landscape).

Out of scope: a version in the logged-in app screens (chat, account); a link to the commit on GitHub (rule 7 allows a link, but the line stays plain text).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `client`: new CLI-72, CLI-73.
- `operations`: new OPS-17.

## Impact

- Deploy: `deploy/pull-deploy.sh` (commit time with `git log -1 --format=%cd`, `TZ=Asia/Tehran`), `deploy/deploy.sh` (writes both lines of `version.env`; the release folder keeps its sha name).
- Host: `Probes.cs` (`commit` in `/health`), `appsettings.json` (`Dalan:Commit` = "").
- Client: `public.ts` (the line), `api.ts` (`/health`), `i18n.ts`, `app.css` (`footer.site` pushed to the end).
- Docs: `docs/UI.md` footer, `docs/SERVER.md` (how to read the live version).
- No new admin settings: the version is set by the build, not by the admin.

**Changed 2026-11-25 (owner):** the version is `v<major>.<minor>.<n>` from `VERSION` and the count of commits since it changed (`v1.1.0`, `v1.1.1`, …), not the commit time; the footer is `© <this year> Dalan v1.1.0` and reads the version from `/api/public/settings`, because `/health` is private (OPS-06) and gave the browser a 404.
