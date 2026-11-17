// Rules: CLI-31, CLI-32
// Runs public/dalan.mjs as a builder would, against a small fake server.
import assert from "node:assert/strict";
import { execFile } from "node:child_process";
import { readFileSync } from "node:fs";
import { createServer, type IncomingMessage } from "node:http";
import { test } from "node:test";

const cli = new URL("../public/dalan.mjs", import.meta.url).pathname;

interface Seen { method: string; path: string; auth: string; body: string }

/** A server that answers every call with this status and body, and remembers the calls. */
async function fakeServer(status: number, answer: unknown): Promise<{ url: string; seen: Seen[]; close: () => void }> {
  const seen: Seen[] = [];
  const server = createServer(async (request: IncomingMessage, response) => {
    let body = "";
    for await (const chunk of request) body += chunk;
    seen.push({ method: request.method!, path: request.url!, auth: request.headers.authorization ?? "", body });
    response.writeHead(status, { "content-type": "application/json" }).end(JSON.stringify(answer));
  });
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  const { port } = server.address() as { port: number };
  return { url: `http://127.0.0.1:${port}`, seen, close: () => server.close() };
}

function run(args: string[], env: Record<string, string>): Promise<{ code: number; stdout: string; stderr: string }> {
  return new Promise(resolve => {
    execFile(process.execPath, [cli, ...args], { env: { PATH: process.env.PATH ?? "", ...env } }, (error, stdout, stderr) =>
      resolve({ code: error ? Number(error.code) : 0, stdout, stderr }));
  });
}

test("CLI-31 create sends the key and the body, and prints the answer", async () => {
  const server = await fakeServer(201, { id: 7, username: "weather_bot", name: "Weather", token: "7:secret" });
  try {
    const result = await run(["create", "weather_bot", "Weather"], { DALAN_URL: server.url, DALAN_KEY: "dalan_k" });

    assert.equal(result.code, 0);
    assert.deepEqual(JSON.parse(result.stdout), { id: 7, username: "weather_bot", name: "Weather", token: "7:secret" });
    assert.deepEqual(server.seen, [{ method: "POST", path: "/api/builder/bots", auth: "Bearer dalan_k", body: '{"username":"weather_bot","name":"Weather"}' }]);
  } finally {
    server.close();
  }
});

test("CLI-31 bots, token and status call their addresses", async () => {
  const server = await fakeServer(200, { ok: true });
  try {
    const env = { DALAN_URL: server.url, DALAN_KEY: "dalan_k" };
    for (const args of [["bots"], ["token", "7"], ["status", "7"]]) assert.equal((await run(args, env)).code, 0);

    assert.deepEqual(server.seen.map(call => `${call.method} ${call.path}`),
      ["GET /api/builder/bots", "POST /api/builder/bots/7/token", "GET /api/builder/bots/7"]);
  } finally {
    server.close();
  }
});

test("CLI-31 an error answer goes to stderr with exit code 1", async () => {
  const server = await fakeServer(409, { error: "username_taken", message: "Another bot has this username. Choose another one." });
  try {
    const result = await run(["create", "weather_bot", "Weather"], { DALAN_URL: server.url, DALAN_KEY: "dalan_k" });

    assert.equal(result.code, 1);
    assert.equal(result.stdout, "");
    assert.deepEqual(JSON.parse(result.stderr), { error: "username_taken", message: "Another bot has this username. Choose another one." });
  } finally {
    server.close();
  }
});

test("CLI-31 no command, a wrong command or no key prints the usage with exit code 2", async () => {
  const env = { DALAN_URL: "http://127.0.0.1:9", DALAN_KEY: "dalan_k" };
  for (const [args, environment] of [[[], env], [["fly"], env], [["token"], env], [["bots"], { DALAN_URL: env.DALAN_URL }]] as const) {
    const result = await run([...args], environment);
    assert.equal(result.code, 2);
    assert.match(result.stderr, /^Usage: node dalan\.mjs/);
  }
});

test("CLI-32 the CLI is one file that needs no package", () => {
  const text = readFileSync(cli, "utf8");
  const imports = [...text.matchAll(/\bimport\b[^;]*?from\s+["']([^"']+)["']|\bimport\(\s*["']([^"']+)["']/g)];
  assert.deepEqual(imports, []);
  assert.doesNotMatch(text, /\brequire\(/);
});
