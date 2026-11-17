// Rules: CLI-05, CLI-06, CLI-07
import assert from "node:assert/strict";
import { test } from "node:test";
import { Chats, currentKeyboard, merge, type Message } from "../src/messages.ts";

const m = (id: number, from: "user" | "bot" = "bot", reply_keyboard?: string[][]): Message =>
  ({ id, from, text: `text ${id}`, date: "2026-11-16T12:00:00Z", ...(reply_keyboard ? { reply_keyboard } : {}) });

const ids = (list: readonly Message[]) => list.map(message => message.id);

/** A fake server: one chat with these messages, pages of `pageSize`, and a record of what was asked. */
function server(all: Message[], pageSize: number) {
  const asked: { after?: number; before?: number }[] = [];
  const load = async (_botId: number, range: { after?: number; before?: number }) => {
    asked.push(range);
    if (range.after !== undefined) return all.filter(x => x.id > range.after!).slice(0, pageSize);
    return all.filter(x => range.before === undefined || x.id < range.before).slice(-pageSize);
  };
  return { asked, load, chats: new Chats(load, pageSize) };
}

test("CLI-05 the same message twice is kept once", () => {
  assert.deepEqual(ids(merge([m(1), m(2)], [m(2), m(3)])), [1, 2, 3]);
});

test("CLI-05 messages that arrive out of order are shown in order", () => {
  assert.deepEqual(ids(merge(merge([m(3)], [m(5)]), [m(4)])), [3, 4, 5]);
});

test("CLI-06 opening a chat asks for the newest messages", async () => {
  const { asked, chats } = server([m(1), m(2), m(3)], 2);

  const chat = await chats.open(7);

  assert.deepEqual(asked, [{}]);
  assert.deepEqual(ids(chat.messages), [2, 3]);
});

test("CLI-06 a reconnect asks for the messages after the newest one", async () => {
  const all = [m(6), m(7)];
  const { asked, chats } = server(all, 10);
  const chat = await chats.open(7);
  all.push(m(8), m(9));

  await chats.sync();

  assert.deepEqual(asked[1], { after: 7 });
  assert.deepEqual(ids(chat.messages), [6, 7, 8, 9]);
});

test("CLI-06 a full page makes the client ask again until a page is not full", async () => {
  const all = [m(1)];
  const { asked, chats } = server(all, 2);
  const chat = await chats.open(7);
  all.push(m(2), m(3), m(4), m(5), m(6));

  await chats.sync();

  assert.deepEqual(asked.slice(1), [{ after: 1 }, { after: 3 }, { after: 5 }]);
  assert.deepEqual(ids(chat.messages), [1, 2, 3, 4, 5, 6]);
});

test("CLI-06 a live message during a sync does not make the sync skip older messages", async () => {
  const all = [m(7)];
  const asked: number[] = [];
  let chats!: Chats;
  chats = new Chats(async (botId, range) => {
    if (range.after === undefined) return all;
    asked.push(range.after);
    // Message 12 arrives live while the first sync answer is on its way.
    if (asked.length === 1) chats.receive(botId, m(12));
    return [m(8), m(9), m(10), m(11), m(12)].filter(x => x.id > range.after!).slice(0, 2);
  }, 2);
  const chat = await chats.open(7);

  await chats.sync();

  assert.deepEqual(asked, [7, 9, 11]);
  assert.deepEqual(ids(chat.messages), [7, 8, 9, 10, 11, 12]);
});

test("CLI-06 a live message for a chat that was never opened does not hide its history", async () => {
  const { asked, chats } = server([m(1), m(2), m(3)], 10);

  chats.receive(7, m(3));
  const chat = await chats.open(7);

  assert.deepEqual(asked, [{}]);
  assert.deepEqual(ids(chat.messages), [1, 2, 3]);
});

test("CLI-06 a live message that arrives while the chat opens is kept", async () => {
  let chats!: Chats;
  chats = new Chats(async botId => {
    chats.receive(botId, m(3));
    return [m(1), m(2)];
  }, 10);

  const chat = await chats.open(7);

  assert.deepEqual(ids(chat.messages), [1, 2, 3]);
});

test("CLI-06 older messages are loaded before the oldest one, until the start", async () => {
  const { asked, chats } = server([m(1), m(2), m(3), m(4), m(5)], 2);
  const chat = await chats.open(7);
  assert.equal(chat.reachedStart, false);

  await chats.loadOlder(7);
  await chats.loadOlder(7);

  assert.deepEqual(asked.slice(1), [{ before: 4 }, { before: 2 }]);
  assert.deepEqual(ids(chat.messages), [1, 2, 3, 4, 5]);
  assert.equal(chat.reachedStart, true);
});

test("CLI-07 the keyboard comes from the newest bot message that has one", () => {
  const list = [m(1, "user"), m(2, "bot", [["Yes", "No"]]), m(3, "user"), m(4, "bot")];

  assert.deepEqual(currentKeyboard(list), [["Yes", "No"]]);
});

test("CLI-07 an empty keyboard removes the buttons", () => {
  assert.deepEqual(currentKeyboard([m(2, "bot", [["Yes"]]), m(4, "bot", [])]), []);
});

test("CLI-07 no keyboard yet means no buttons", () => {
  assert.deepEqual(currentKeyboard([m(1, "user"), m(2, "bot")]), []);
});
