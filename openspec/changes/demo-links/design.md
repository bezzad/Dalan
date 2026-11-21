# Design

## Address

`https://<Sites:DemoHost>/<id>/`. `DemoHost` is its own setting (empty: demos are off), `dalans.ir` in production and `demo.localhost` in local work, where the bare `localhost` stays the Dalan app. The id is `Sites:DemoIdLength` (start 14) random characters `[a-z0-9]`, about 72 bits: not guessable.

## Routing on the demo host

1. `/<id>` with a live demo: redirect to `/<id>/`.
2. `/<id>/rest`: forward `/rest` (and the query) to the demo; set `dalan_demo=<id>; Path=/; Secure; HttpOnly; SameSite=Lax` until the demo ends.
3. Any other path with a `dalan_demo` cookie of a live demo: forward the path unchanged to that demo.
4. Else: 404 page.

The demo host sits before Dalan's own pipeline, like the site hosts (`UseSites`). Request size, timeouts and the per-address limit are the sites ones.

## Data and session

Table `sites.demos (id text primary key, owner_user_id, created_at, expires_at, ended_at)`. The row stays after the end, so the owner of an address can be named later (access log duty). The tunnel session carries the demo id next to its site names: `POST /api/tunnel/sessions` with `{"demo": "new"}` makes a demo, `{"demo": "<id>"}` re-attaches after a reconnect (same owner, not ended). `DELETE /api/tunnel/demos/<id>` ends it. The agent's forwarder sees the demo id in `TunnelRequest.Site`, as `demo:<id>`.

## Risk

All demos share one origin (`dalans.ir`): a demo's script can read what another demo stored in the same browser. Demos are for tests; the agent says "do not use real accounts or secrets in a demo". Dalan itself is on another registered domain (SITE-13), so a demo cannot touch Dalan's cookies.
