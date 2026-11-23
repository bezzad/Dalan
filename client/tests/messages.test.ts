// Rules: CLI-05, CLI-06, CLI-07, CLI-13, CLI-14, CLI-15, CLI-16, CLI-17
import assert from "node:assert/strict";
import { test } from "node:test";
import {
  buttonAction, Chat, Chats, clock, commandMessage, currentKeyboard, fileView, firstFile, fitsSize, KeyboardState, merge, replyKeyboard, stamp,
  type FileInfo, type Message,
} from "../src/messages.ts";

/** A message whose version is its id, as when nothing was changed. */
const m = (id: number, from: "user" | "bot" = "bot", reply_keyboard?: string[][]): Message =>
  ({ id, version: id, from, text: `text ${id}`, date: "2026-11-16T12:00:00Z", ...(reply_keyboard ? { reply_keyboard } : {}) });

/** A change of message `id` that got version `version`. */
const change = (id: number, version: number, more: Partial<Message> = {}): Message => ({ ...m(id), version, ...more });

const ids = (list: readonly Message[]) => list.map(message => message.id);

/** A fake server: one chat with these messages, pages of `pageSize`, and a record of what was asked. */
function server(all: Message[], pageSize: number, pinned: Message | null = null) {
  const asked: { after?: number; before?: number }[] = [];
  const load = async (_botId: number, range: { after?: number; before?: number }) => {
    asked.push(range);
    const messages = range.after !== undefined
      ? all.filter(x => x.version > range.after!).sort((a, b) => a.version - b.version).slice(0, pageSize)
      : all.filter(x => range.before === undefined || x.id < range.before).slice(-pageSize);
    return { messages, pinned };
  };
  return { asked, load, chats: new Chats(load, pageSize) };
}

test("CLI-05 the same message twice is kept once", () => {
  assert.deepEqual(ids(merge([m(1), m(2)], [m(2), m(3)])), [1, 2, 3]);
});

test("CLI-05 messages that arrive out of order are shown in order", () => {
  assert.deepEqual(ids(merge(merge([m(3)], [m(5)]), [m(4)])), [3, 4, 5]);
});

test("CLI-05 an edit wins over the old copy, even when the old copy comes later", () => {
  const chat = new Chat();
  chat.add([m(1), m(2)]);

  chat.add([change(2, 5, { text: "new" })]);
  chat.add([m(2)]);

  assert.equal(chat.visible[1]!.text, "new");
});

test("CLI-05 a deleted message leaves the list", () => {
  const chat = new Chat();
  chat.add([m(1), m(2)]);

  chat.add([change(2, 3, { deleted: true })]);

  assert.deepEqual(ids(chat.visible), [1]);
});

test("CLI-05 a change to a message older than the loaded ones is not added", () => {
  const chat = new Chat();
  chat.add([m(50), m(51)]);

  chat.add([change(3, 60, { text: "edited" })]);

  assert.deepEqual(ids(chat.visible), [50, 51]);
  assert.equal(chat.version, 60);
});

test("CLI-06 opening a chat asks for the newest messages", async () => {
  const { asked, chats } = server([m(1), m(2), m(3)], 2);

  const chat = await chats.open(7);

  assert.deepEqual(asked, [{}]);
  assert.deepEqual(ids(chat.messages), [2, 3]);
});

test("CLI-06 a reconnect asks for the changes after the highest version", async () => {
  const all = [m(6), m(7)];
  const { asked, chats } = server(all, 10);
  const chat = await chats.open(7);
  all.push(m(8), change(6, 9, { text: "edited" }));

  await chats.sync();

  assert.deepEqual(asked[1], { after: 7 });
  assert.deepEqual(ids(chat.messages), [6, 7, 8]);
  assert.equal(chat.messages[0]!.text, "edited");
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

test("CLI-06 a live message during a sync does not make the sync skip older changes", async () => {
  const asked: number[] = [];
  let chats!: Chats;
  chats = new Chats(async (botId, range) => {
    if (range.after === undefined) return { messages: [m(7)], pinned: null };
    asked.push(range.after);
    // Version 12 arrives live while the first sync answer is on its way.
    if (asked.length === 1) chats.receive(botId, m(12));
    return { messages: [m(8), m(9), m(10), m(11), m(12)].filter(x => x.version > range.after!).slice(0, 2), pinned: null };
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
    return { messages: [m(1), m(2)], pinned: null };
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

test("CLI-07 the keyboard of a deleted message is not used", () => {
  assert.deepEqual(currentKeyboard([m(1, "bot", [["Old"]]), { ...m(2, "bot", [["New"]]), deleted: true }]), [["Old"]]);
});

test("CLI-13 a callback button sends a press and a url button opens the link", () => {
  assert.deepEqual(buttonAction({ text: "A", callback_data: "a" }), { press: "a" });
  assert.deepEqual(buttonAction({ text: "Site", url: "https://dalanet.ir" }), { open: "https://dalanet.ir" });
});

test("CLI-14 the pinned message of the newest page answer is the bar, even when it is older than the page", async () => {
  const pinned = { ...m(1), pinned: true };
  const { chats } = server([pinned, m(2), m(3)], 2, pinned);

  const chat = await chats.open(7);

  assert.equal(chat.pinned?.id, 1);
});

test("CLI-14 a click on the bar loads older pages until the pinned message is there", async () => {
  const pinned = { ...m(2), pinned: true };
  const { chats } = server([m(1), pinned, m(3), m(4), m(5), m(6)], 2, pinned);
  await chats.open(7);

  assert.equal(await chats.find(7, 2), true);
  assert.ok(ids(chats.of(7).visible).includes(2));
  assert.equal(await chats.find(7, 5), true, "a loaded message needs no page");
});

test("CLI-14 a pinned message older than the history loads to the start and is not found", async () => {
  const { chats } = server([m(3), m(4), m(5)], 2, { ...m(1), pinned: true });
  await chats.open(7);

  assert.equal(await chats.find(7, 1), false);
  assert.ok(chats.of(7).reachedStart);
});

test("CLI-14 the pin moves to the newly pinned message", () => {
  const chat = new Chat();
  chat.add([m(1), { ...m(2), pinned: true }]);

  chat.add([change(2, 3)]);
  chat.add([change(1, 4, { pinned: true })]);

  assert.equal(chat.pinned?.id, 1);
});

test("CLI-14 an unpin or a delete clears the bar", () => {
  const chat = new Chat();
  chat.add([{ ...m(2), pinned: true }]);
  chat.add([change(2, 3)]);
  assert.equal(chat.pinned, null);

  chat.add([change(2, 4, { pinned: true })]);
  chat.add([change(2, 5, { pinned: true, deleted: true })]);
  assert.equal(chat.pinned, null);
});

test("CLI-15 choosing a command sends it as a message", () => {
  assert.equal(commandMessage("help"), "/help");
});

test("CLI-16 each kind of file has its view", () => {
  const file = (kind: FileInfo["kind"]): FileInfo => ({ id: "x", kind, name: "f", type: "", size: 1 });

  assert.deepEqual(["photo", "voice", "audio", "video", "document"].map(kind => fileView(file(kind as FileInfo["kind"]))),
    ["image", "player", "player", "video", "link"]);
});

test("CLI-16 a voice length reads as minutes and seconds", () => {
  assert.equal(clock(65), "1:05");
  assert.equal(clock(7), "0:07");
  assert.equal(clock(3723), "1:02:03");
  assert.equal(clock(-1), "0:00");
});

test("CLI-47 a pasted screenshot gets a name; text alone is no file", () => {
  const rename = (file: File, name: string) => new File([file], name, { type: file.type });
  assert.equal(firstFile([new File(["x"], "image.png", { type: "image/png" })], rename)?.name, "image.png");
  assert.equal(firstFile([new File(["x"], "", { type: "image/jpeg" })], rename)?.name, "image.jpg");
  assert.equal(firstFile([new File(["x"], "report.pdf", { type: "application/pdf" })], rename)?.name, "report.pdf");
  assert.equal(firstFile([], rename), null);
});

test("CLI-07 rows stay as sent, with their styles and options", () => {
  const rows = [["1", "2", "3", "4", "5"], ["6", "7", "8", "9"], ["a", "b", "➕"]];
  const message: Message = { ...m(3, "bot", rows), reply_keyboard_options: { resize: true, styles: [[null, null, null, null, null], [null, null, null, null], [null, null, "success"]] } };
  const keyboard = replyKeyboard([m(1, "bot", [["Old"]]), message])!;
  assert.deepEqual(keyboard.rows.map(row => row.length), [5, 4, 3]);
  assert.equal(keyboard.rows[2]![2]!.style, "success");
  assert.equal(keyboard.rows[0]![0]!.style, null);
  assert.deepEqual([keyboard.messageId, keyboard.resize, keyboard.persistent, keyboard.oneTime], [3, true, false, false]);
  assert.equal(replyKeyboard([message, m(4, "bot", [])]), null);
});

test("CLI-07 a one-time keyboard hides after a tap and the ⌨ button brings it back; a new one shows again", () => {
  const state = new KeyboardState();
  const oneTime = replyKeyboard([{ ...m(2, "bot", [["Yes"]]), reply_keyboard_options: { one_time: true } }])!;
  assert.equal(state.visible(oneTime), true);
  state.tapped(oneTime);
  assert.equal(state.visible(oneTime), false);
  assert.equal(state.hasToggle(oneTime), true);
  state.toggle(oneTime);
  assert.equal(state.visible(oneTime), true);
  state.toggle(oneTime);
  assert.equal(state.visible(oneTime), false);
  assert.equal(state.visible(replyKeyboard([m(5, "bot", [["New"]])])), true);
});

test("CLI-07 a persistent keyboard cannot hide and has no ⌨ button", () => {
  const state = new KeyboardState();
  const persistent = replyKeyboard([{ ...m(2, "bot", [["Yes"]]), reply_keyboard_options: { persistent: true, one_time: true } }])!;
  state.tapped(persistent);
  assert.equal(state.visible(persistent), true);
  assert.equal(state.hasToggle(persistent), false);
});

test("CLI-16 a file over the limit is refused before it is sent", () => {
  assert.equal(fitsSize(20 * 1024 * 1024, 20), true);
  assert.equal(fitsSize(20 * 1024 * 1024 + 1, 20), false);
});

test("CLI-17 an edited message has the edited mark", () => {
  const words = { edited: "edited", notDelivered: "not delivered" };
  assert.equal(stamp(m(1), "12:00", words), "12:00");
  assert.equal(stamp({ ...m(1), edited: "2026-11-16T12:05:00Z" }, "12:00", words), "12:00 · edited");
});
