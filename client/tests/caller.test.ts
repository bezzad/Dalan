// Rule: CLI-11
import assert from "node:assert/strict";
import { test } from "node:test";
import { ApiError, createCaller, type Send } from "../src/caller.ts";

/** A server that answers each path with the next status of its list (the last one repeats). */
function fakeServer(answers: Record<string, number[]>) {
  const calls: string[] = [];
  const send: Send = async path => {
    calls.push(path);
    const list = answers[path] ?? [200];
    const status = list.length > 1 ? list.shift()! : list[0]!;
    return {
      status,
      ok: status < 400,
      headers: { get: name => (name === "Retry-After" && status === 429 ? "40" : null) },
      json: async () => (status < 400 ? { path } : { error: status === 401 ? "unauthorized" : "rate_limited" }),
    };
  };
  return { calls, call: createCaller(send) };
}

test("CLI-11 a 401 is followed by one refresh and a second try", async () => {
  const server = fakeServer({ "/api/bots": [401, 200], "/api/auth/refresh": [204] });

  const answer = await server.call<{ path: string }>("GET", "/api/bots");

  assert.deepEqual(server.calls, ["/api/bots", "/api/auth/refresh", "/api/bots"]);
  assert.equal(answer.path, "/api/bots");
});

test("CLI-11 two calls that fail at the same moment share one refresh", async () => {
  const server = fakeServer({ "/api/me": [401, 200], "/api/bots": [401, 200], "/api/auth/refresh": [204] });

  await Promise.all([server.call("GET", "/api/me"), server.call("GET", "/api/bots")]);

  assert.equal(server.calls.filter(path => path === "/api/auth/refresh").length, 1);
  assert.equal(server.calls.length, 5);
});

test("CLI-11 a later 401 starts a new refresh", async () => {
  const server = fakeServer({ "/api/me": [401, 200, 401, 200], "/api/auth/refresh": [204] });

  await server.call("GET", "/api/me");
  await server.call("GET", "/api/me");

  assert.equal(server.calls.filter(path => path === "/api/auth/refresh").length, 2);
});

test("CLI-11 when the session is over the 401 reaches the caller", async () => {
  const server = fakeServer({ "/api/me": [401], "/api/auth/refresh": [401] });

  await assert.rejects(server.call("GET", "/api/me"), (error: unknown) => error instanceof ApiError && error.status === 401);
  assert.deepEqual(server.calls, ["/api/me", "/api/auth/refresh", "/api/me"]);
});

test("CLI-11 a wrong password or code is not a reason to refresh", async () => {
  for (const path of ["/api/auth/password", "/api/auth/sms/verify", "/api/auth/refresh", "/api/dev/login", "/api/logout"]) {
    const server = fakeServer({ [path]: [401] });

    await assert.rejects(server.call("POST", path, {}));
    assert.deepEqual(server.calls, [path]);
  }
});

test("CLI-11 an error carries the code and the seconds to wait", async () => {
  const server = fakeServer({ "/api/auth/sms": [429] });

  await assert.rejects(server.call("POST", "/api/auth/sms", {}), (error: unknown) =>
    error instanceof ApiError && error.code === "rate_limited" && error.retryAfter === 40);
});
