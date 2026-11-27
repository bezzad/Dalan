# Design

## Streamed answers (SITE-22, AGT-12)

The answer message gets one more field, `more` (false when missing, so an older agent still works):

- First message of an answer: `{"id":7,"status":200,"headers":{...},"body":"<base64 or null>","more":true}`.
- Next messages: `{"id":7,"status":0,"body":"<base64>","more":true}`; the last one has `"more":false` (its body may be null).
- An answer in one message is as before (`more` false).

The agent reads the local answer and sends a message when it has `Sites:StreamChunkKb` bytes, when the answer ends, or when no new byte came for a short moment (20 ms). So a small answer is still one message, and a Server-Sent Event reaches the visitor at once.

`SessionOpened` gives the agent `stream_chunk_kb` and `stream_window`. The server writes each part to the visitor and flushes it.

**Flow control.** One slow visitor must not fill the server's memory.

- WebSocket: the agent has at most `stream_window` parts of one answer on the way. After the server wrote a part to the visitor it sends `{"id":7,"ack":true}`; each ack lets the agent send one more. A part beyond the window ends that answer.
- Long polling: each part is one `POST /responses`; the server answers it only after the part was written to the visitor. So the agent sends one part after the other.

**Stop.** When the visitor leaves, the answer goes over `Sites:MaxAnswerMb`, or the session ends:

- WebSocket: the server sends `{"id":7,"cancel":true}`; the agent stops reading the local answer.
- Long polling: the `POST` of the next part gets 410 `request_gone`; the agent stops.

A visitor whose stream stopped in the middle sees a broken connection, not a short answer that looks whole.

**Time.** `Sites:RequestTimeoutSeconds` is the wait for the first message. A stream that has started may run longer; it ends when the app ends it, the visitor leaves or the session ends.

A message from the server to the agent is told apart by its fields: a request has `method`; `cancel` and `ack` are control messages. An older agent never streams, so it never gets them.

## The line per address (SITE-12)

One lock and a map of gates: for each address, the number of requests at the agent and the line of waiting requests (in order). A request takes a free place, or waits in the line. When a request ends, its place goes to the first one in the line. A waiting request that leaves (timeout or visitor gone) takes itself out of the line. A gate with nothing running and nothing waiting is removed.

The setting can change at run time; a smaller limit only stops new requests from entering.

## Session end (SITE-23, AGT-13)

`DELETE /api/tunnel/sessions/{session}` ends the caller's session (`hub.End`). The agent calls it on Ctrl+C, with a 5-second limit; a failure is only said, the session ends by idle time anyway.

The agent opens a new session after any lost WebSocket. So when the WebSocket of a session closes, the server ends the session too. Visitors waiting on it get the offline page at once, not a timeout.

## Service (AGT-14, AGT-15)

`dalan service install` uses the config file of the current user and the path of the running agent file:

- Linux: `~/.config/systemd/user/dalan.service` (`ExecStart=<agent> start`, `Restart=always`), then `systemctl --user daemon-reload` and `systemctl --user enable --now dalan`. Then `loginctl enable-linger` so it runs without a login; when that fails, the agent says the site runs only while the user is logged in.
- macOS: `~/Library/LaunchAgents/ir.dalanet.agent.plist` (`RunAtLoad`, `KeepAlive`, log to `~/Library/Logs/dalan.log`), then `launchctl bootstrap gui/<uid>`.
- Windows: the value `Dalan` = `"<agent>" start --background` under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` (a logon task needs admin), then the agent starts at once. With `--background` it closes its console window and writes to `dalan.log` next to the config file. Windows does not start it again after a crash; the agent itself already reconnects after every network loss.

`dalan service uninstall` stops and removes it. `install` needs a key and at least one saved site first. The agent runs the OS commands through one small runner, so tests check the files and commands without changing the test machine.

`dalan status` prints the service state (the OS answer) and then the builder's sites from `GET /api/sites` with the key: name, address, online or offline.

## Port in the sites domain

`Sites:Domain` `localhost:5000` → site address `http://mysite.localhost:5000`; the host match uses only the part before `:`. SITE-13 compares hosts without port. Production values have no port, so nothing changes there.

## Reserve (SITE-24)

`POST /api/sites {"name":"mysite"}` runs the same claim as a session (`SiteBook.ClaimAsync`): 201 with the site, or 400 `bad_name`, 409 `name_taken`, 409 `site_limit`. A name the caller already owns gives 200. My apps polls `GET /api/sites` every 10 seconds while the screen is open, so "online" follows the agent.
