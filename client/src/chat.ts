// The chat with one bot: history, live messages, reply buttons (CLI-07) and the input.
import { api, type Bot } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { h } from "./dom.ts";
import { currentKeyboard, type Message } from "./messages.ts";

export function chatScreen(app: App, username: string): Screen {
  const t = app.language.t.bind(app.language);
  const title = h("h1", { dir: "auto" }, `@${username}`);
  const older = h("button", { type: "button", class: "link", hidden: true }, t("older"));
  const list = h("ol", { class: "messages" });
  const scroller = h("div", { class: "scroller" }, older, list);
  const keyboard = h("div", { class: "keyboard" });
  const error = h("p", { class: "error", role: "alert" });
  const input = h("textarea", { rows: "1", dir: "auto", maxlength: String(app.maxTextLength), placeholder: t("message"), "aria-label": t("message") });
  const sendButton = h("button", { type: "submit", class: "primary" }, t("send"));
  const start = h("button", { type: "button", class: "primary start", hidden: true }, t("start"));
  const form = h("form", { class: "row", hidden: true }, input, sendButton);
  let bot: Bot | null = null;

  function bubble(message: Message): HTMLElement {
    return h("li", { class: message.from },
      h("p", { dir: "auto" }, message.text),
      h("time", { datetime: message.date }, app.language.time(message.date)));
  }

  function draw(keepPlace = false): void {
    if (!bot) return;
    const chat = app.chats.of(bot.id);
    const atEnd = scroller.scrollHeight - scroller.scrollTop - scroller.clientHeight < 80;
    const heightBefore = scroller.scrollHeight;

    list.replaceChildren(...chat.messages.map(bubble));
    older.hidden = chat.reachedStart || chat.messages.length === 0;
    keyboard.replaceChildren(...currentKeyboard(chat.messages).map(row =>
      h("div", { class: "row" }, ...row.map(text =>
        h("button", { type: "button", dir: "auto", onclick: () => void send(text) }, text)))));

    const started = chat.messages.length > 0;
    start.hidden = started;
    form.hidden = !started;

    // Older messages were added on top: stay at the same message. Otherwise follow the newest one.
    if (keepPlace) scroller.scrollTop += scroller.scrollHeight - heightBefore;
    else if (atEnd) scroller.scrollTop = scroller.scrollHeight;
  }

  async function send(text: string): Promise<void> {
    if (!bot || text.trim() === "") return;
    error.textContent = "";
    sendButton.disabled = true;
    try {
      app.chats.receive(bot.id, await api.send(bot.id, text));
      input.value = "";
      draw();
      scroller.scrollTop = scroller.scrollHeight;
    } catch (failure) {
      error.textContent = t(errorText(failure));
    } finally {
      sendButton.disabled = false;
    }
  }

  form.addEventListener("submit", event => {
    event.preventDefault();
    void send(input.value);
  });
  input.addEventListener("keydown", event => {
    if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      void send(input.value);
    }
  });
  start.addEventListener("click", () => void send("/start"));
  older.addEventListener("click", async () => {
    if (!bot) return;
    try {
      await app.chats.loadOlder(bot.id);
      draw(true);
    } catch (failure) {
      error.textContent = t(errorText(failure));
    }
  });

  (async () => {
    try {
      bot = await api.findBot(username);
      title.textContent = bot.name;
      await app.chats.open(bot.id);
      draw();
      scroller.scrollTop = scroller.scrollHeight;
    } catch (failure) {
      error.textContent = t(errorText(failure));
    }
  })();

  const element = h("main", { class: "chat" },
    h("header", {},
      h("button", { type: "button", class: "link", onclick: () => app.go("/") }, t("back")),
      title,
      h("span", { class: "note", dir: "ltr" }, `@${username}`)),
    scroller,
    error,
    keyboard,
    start,
    form);

  return { element, onMessage: botId => { if (botId === bot?.id) draw(); } };
}
