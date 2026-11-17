#!/usr/bin/env node
// A tiny bot to try Dalan on your own machine: it answers every message and shows reply buttons.
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

const answers = {
  "/start": ["سلام! یکی را انتخاب کنید.", [["ساعت", "تاس"], ["بستن دکمه‌ها"]]],
  "ساعت": [() => new Date().toLocaleTimeString("fa-IR"), undefined],
  "تاس": [() => `🎲 ${1 + Math.floor(Math.random() * 6)}`, undefined],
  "بستن دکمه‌ها": ["دکمه‌ها بسته شد. برای باز شدن /start بفرستید.", []],
};

let offset = 0;
console.log(`Demo bot is polling ${api} ...`);
for (;;) {
  try {
    const response = await fetch(`${api}/v1/updates?offset=${offset}&timeout=30`, { headers });
    if (!response.ok) throw new Error(`${response.status} ${await response.text()}`);

    for (const update of (await response.json()).updates) {
      offset = update.update_id + 1;
      const { from, text } = update.message;
      const [answer, reply_keyboard] = answers[text] ?? [`شما نوشتید: ${text}`, undefined];
      console.log(`${from.name} (${from.id}): ${text}`);
      await fetch(`${api}/v1/messages`, {
        method: "POST",
        headers,
        body: JSON.stringify({ user_id: from.id, text: typeof answer === "function" ? answer() : answer, reply_keyboard }),
      });
    }
  } catch (error) {
    console.error(String(error));
    await new Promise(resolve => setTimeout(resolve, 3000));
  }
}
