#!/usr/bin/env node
// Dalan CLI (CLI-31, CLI-32): one file, Node 18 or newer, no packages.
// Get it from your Dalan site:  curl -O <site>/dalan.mjs
// Needs DALAN_URL (the site address) and DALAN_KEY (an access key from "My bots").

const usage = `Usage: node dalan.mjs <command>

Commands:
  bots                       list your bots
  create <username> <name>   create a bot; prints its token once
  token <bot id>             give a bot a new token; the old one stops at once
  status <bot id>            is the bot online, and when was it last seen

Environment:
  DALAN_URL   the address of the Dalan site
  DALAN_KEY   an access key (dalan_...), made in "My bots" on the site

Output is JSON on stdout. An error is JSON on stderr with exit code 1.`;

const commands = {
  bots: { args: 0, method: "GET", path: () => "/api/builder/bots" },
  create: { args: 2, method: "POST", path: () => "/api/builder/bots", body: ([username, name]) => ({ username, name }) },
  token: { args: 1, method: "POST", path: ([id]) => `/api/builder/bots/${encodeURIComponent(id)}/token` },
  status: { args: 1, method: "GET", path: ([id]) => `/api/builder/bots/${encodeURIComponent(id)}` },
};

const [name, ...args] = process.argv.slice(2);
const command = Object.hasOwn(commands, name ?? "") ? commands[name] : undefined;
const { DALAN_URL: site, DALAN_KEY: key } = process.env;
if (!command || args.length !== command.args || !site || !key) {
  console.error(usage);
  process.exit(2);
}

try {
  const response = await fetch(new URL(command.path(args), site), {
    method: command.method,
    headers: { authorization: `Bearer ${key}`, "content-type": "application/json" },
    body: command.body ? JSON.stringify(command.body(args)) : undefined,
  });
  const text = await response.text();
  let answer;
  try {
    answer = JSON.parse(text);
  } catch {
    answer = { error: "bad_answer", message: `The server answered ${response.status} without JSON. Check DALAN_URL.` };
  }
  if (!response.ok) {
    if (response.status === 401) answer = { error: "unauthorized", message: "The access key is wrong, deleted or expired. Make a new one in \"My bots\"." };
    console.error(JSON.stringify({ error: answer.error ?? "error", message: answer.message ?? `HTTP ${response.status}` }));
    process.exit(1);
  }
  console.log(JSON.stringify(answer, null, 2));
} catch (failure) {
  console.error(JSON.stringify({ error: "no_connection", message: `Could not reach ${site}: ${failure.message}` }));
  process.exit(1);
}
