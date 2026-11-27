# Design

## Decisions

1. **Format.** The usual copyright form is the sign, the year and the name: `© 2026 Dalan` (not "@copyright"; "All rights reserved" adds nothing). The version follows ArcaneBot: `v<yy.mm.dd.HHMMSS>` from the commit time, so it always rises and two builds never share one. The short sha is only in the tooltip and in `/health`.
2. **One source.** The server writes the version when it builds (OPS-16); the client asks `/health`, so the page and the server can never disagree. No build-time value in the client bundle.
3. **Tehran time.** The owner and the server are in Iran; ArcaneBot uses local time too.
4. **Release folder.** It keeps the 12-character sha as its name: `pull-deploy.sh` uses it to skip a commit that is built or failed.
5. **Footer at the bottom.** `.scroller.page` is a column that fills the screen; `footer.site` gets `margin-block-start: auto`, so free space goes above it. No `position: fixed` or `sticky` on the footer.
6. **`/health` from the page.** It needs no session and has no rate limit; one call per page load is cheap. When it fails, the line shows only `© <year> Dalan`, with the current year.
