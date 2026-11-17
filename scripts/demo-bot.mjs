#!/usr/bin/env node
// A tiny bot to try Dalan on your own machine: it answers every message and file, and shows reply and inline buttons.
// It only calls out (long polling), like a real bot at home.
//
// 1. Make the bot in the local database (there is no "create bot" screen before milestone 8):
//      docker compose exec postgres psql -U dalan -c "INSERT INTO relay.bots (username, name, owner_user_id, token_hash, created_at) VALUES ('demo', 'Demo bot', 0, sha256('local-demo-secret-not-for-production'), now()) RETURNING id"
// 2. Run it with the id that came back (1 on a new database):
//      node scripts/demo-bot.mjs 1
// 3. Open http://localhost:5000/@demo and press Start.
const api = process.env.DALAN_API ?? "http://localhost:5000";
const token = `${process.argv[2] ?? 1}:local-demo-secret-not-for-production`;
const headers = { Authorization: `Bearer ${token}`, "Content-Type": "application/json" };

const call = (method, path, body) =>
  fetch(`${api}${path}`, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });

const answers = {
  "/start": ["سلام! یکی را انتخاب کنید.", [["ساعت", "تاس"], ["دکمه‌های داخل پیام", "بستن دکمه‌ها"]]],
  "/help": ["یک پیام یا فایل بفرستید تا همان را پس بدهم.", undefined],
  "ساعت": [() => new Date().toLocaleTimeString("fa-IR"), undefined],
  "تاس": [() => `🎲 ${1 + Math.floor(Math.random() * 6)}`, undefined],
  "بستن دکمه‌ها": ["دکمه‌ها بسته شد. برای باز شدن /start بفرستید.", []],
};

// Inline buttons: a counter that edits its own message, a pin, and a link.
const counter = count => [[{ text: `شمارنده: ${count} ➕`, callback_data: String(count + 1) }],
  [{ text: "📌 سنجاق", callback_data: "pin" }, { text: "سایت دالان", url: "https://dalanet.ir" }]];

await call("PUT", "/v1/commands", { commands: [{ command: "start", description: "شروع" }, { command: "help", description: "راهنما" }] });

let offset = 0;
console.log(`Demo bot is polling ${api} ...`);
for (;;) {
  try {
    const response = await fetch(`${api}/v1/updates?offset=${offset}&timeout=30`, { headers });
    if (!response.ok) throw new Error(`${response.status} ${await response.text()}`);

    for (const update of (await response.json()).updates) {
      offset = update.update_id + 1;
      if (update.callback) {
        const { id, from, message, data } = update.callback;
        console.log(`${from.name} (${from.id}) pressed ${data}`);
        if (data === "pin") await call("PUT", `/v1/chats/${from.id}/pin`, { message_id: message.id });
        else await call("PATCH", `/v1/messages/${from.id}/${message.id}`, { text: "با هر بار زدن، این پیام ویرایش می‌شود.", inline_keyboard: counter(Number(data)) });
        await call("POST", `/v1/callbacks/${id}/answer`, { text: data === "pin" ? "سنجاق شد" : "یکی اضافه شد" });
        continue;
      }

      const { from, text, file } = update.message;
      console.log(`${from.name} (${from.id}): ${text}${file ? ` + ${file.name}` : ""}`);
      if (file) {
        await call("POST", "/v1/messages", { user_id: from.id, text: `فایل شما: ${file.name}`, file_id: file.id, kind: file.kind });
      } else if (text === "دکمه‌های داخل پیام") {
        await call("POST", "/v1/messages", { user_id: from.id, text: "با هر بار زدن، این پیام ویرایش می‌شود.", inline_keyboard: counter(0) });
      } else {
        const [answer, reply_keyboard] = answers[text] ?? [`شما نوشتید: ${text}`, undefined];
        await call("POST", "/v1/messages", { user_id: from.id, text: typeof answer === "function" ? answer() : answer, reply_keyboard });
      }
    }
  } catch (error) {
    console.error(String(error));
    await new Promise(resolve => setTimeout(resolve, 3000));
  }
}
