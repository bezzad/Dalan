// The chat with one bot: history, live messages, reply buttons (CLI-07), inline buttons (CLI-13),
// the pinned bar (CLI-14), the command menu (CLI-15), files (CLI-16), the read position (CLI-18),
// the bot's status (CLI-19), the "not delivered" mark (CLI-20) and the input.
import { api, type Bot } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { h } from "./dom.ts";
import {
  buttonAction, commandMessage, currentKeyboard, fileView, fitsSize, ReadPosition, stamp, type FileInfo, type InlineButton, type Message,
} from "./messages.ts";

export function chatScreen(app: App, username: string): Screen {
  const t = app.language.t.bind(app.language);
  const title = h("h1", { dir: "auto" }, `@${username}`);
  const status = h("p", { class: "note status", dir: "auto", role: "status" });
  const read = new ReadPosition();
  const pinBar = h("p", { class: "pinned", dir: "auto", hidden: true });
  const older = h("button", { type: "button", class: "link", hidden: true }, t("older"));
  const list = h("ol", { class: "messages" });
  const scroller = h("div", { class: "scroller" }, older, list);
  const keyboard = h("div", { class: "keyboard" });
  const error = h("p", { class: "error", role: "alert" });
  const note = h("p", { class: "note", role: "status" });
  const menu = h("div", { class: "keyboard commands", hidden: true });
  const menuButton = h("button", { type: "button", hidden: true, "aria-label": t("commands"), title: t("commands") }, "☰");
  const picker = h("input", { type: "file", hidden: true });
  const attach = h("button", { type: "button", "aria-label": t("attach"), title: t("attach") }, "📎");
  const input = h("textarea", { rows: "1", dir: "auto", maxlength: String(app.maxTextLength), placeholder: t("message"), "aria-label": t("message") });
  const sendButton = h("button", { type: "submit", class: "primary" }, t("send"));
  const start = h("button", { type: "button", class: "primary start", hidden: true }, t("start"));
  const form = h("form", { class: "row", hidden: true }, menuButton, attach, picker, input, sendButton);
  let bot: Bot | null = null;
  /** True while the newest message is in view; then new messages and loaded pictures keep it in view. */
  let following = true;
  const toEnd = () => { scroller.scrollTop = scroller.scrollHeight; };

  function fileElement(file: FileInfo): HTMLElement {
    const src = api.fileUrl(bot!.id, file.id);
    switch (fileView(file)) {
      case "image": return h("img", { src, alt: file.name, onload: () => { if (following) toEnd(); } });
      case "audio": return h("audio", { src, controls: true, preload: "none" });
      case "video": return h("video", { src, controls: true, preload: "metadata", onloadedmetadata: () => { if (following) toEnd(); } });
      default: return h("a", { href: src, download: file.name, dir: "auto" }, `📄 ${file.name} (${size(file.size)})`);
    }
  }

  function inlineButton(message: Message, button: InlineButton): HTMLElement {
    const action = buttonAction(button);
    // A link opens in a new tab and does not tell the bot's site where it came from.
    if ("open" in action) {
      return h("a", { class: "button", href: action.open, target: "_blank", rel: "noopener noreferrer", dir: "auto" }, button.text);
    }
    return h("button", { type: "button", dir: "auto", onclick: () => void press(message.id, action.press) }, button.text);
  }

  function bubble(message: Message): HTMLElement {
    const time = stamp(message, app.language.time(message.date), { edited: t("edited"), notDelivered: t("notDelivered") });
    return h("li", { class: message.from },
      ...(message.file ? [fileElement(message.file)] : []),
      ...(message.text ? [h("p", { dir: "auto" }, message.text)] : []),
      ...(message.inline_keyboard ?? []).map(row => h("div", { class: "row inline" }, ...row.map(button => inlineButton(message, button)))),
      h("time", { datetime: message.date }, time));
  }

  function draw(keepPlace = false): void {
    if (!bot) return;
    const chat = app.chats.of(bot.id);
    const messages = chat.visible;
    const heightBefore = scroller.scrollHeight;

    pinBar.hidden = !chat.pinned;
    pinBar.textContent = chat.pinned ? `📌 ${chat.pinned.text || chat.pinned.file?.name || ""}` : "";
    list.replaceChildren(...messages.map(bubble));
    older.hidden = chat.reachedStart || messages.length === 0;
    keyboard.replaceChildren(...currentKeyboard(messages).map(row =>
      h("div", { class: "row" }, ...row.map(text =>
        h("button", { type: "button", dir: "auto", onclick: () => void send(text) }, text)))));

    const started = chat.messages.length > 0;
    start.hidden = started;
    form.hidden = !started;

    // Older messages were added on top: stay at the same message. Otherwise follow the newest one.
    if (keepPlace) scroller.scrollTop += scroller.scrollHeight - heightBefore;
    else if (following) toEnd();
    markRead();
  }

  /** CLI-18: tells the server how far the user has read, while the page is visible. */
  function markRead(): void {
    const newest = bot ? app.chats.of(bot.id).messages.at(-1)?.id : undefined;
    if (newest === undefined) return;
    const id = read.next(newest, document.visibilityState === "visible");
    if (id !== null) void api.read(bot!.id, id).catch(() => {});
  }

  /** CLI-19: "online" or "last seen ...". A failed call leaves the line as it was. */
  async function refreshStatus(): Promise<void> {
    if (!bot) return;
    try {
      status.textContent = app.language.seen(await api.status(bot.id), new Date());
    } catch {
      // The next round tries again.
    }
  }

  // Both stop once this screen is gone.
  const statusTimer = window.setInterval(() => {
    if (element.isConnected) void refreshStatus();
    else window.clearInterval(statusTimer);
  }, app.statusRefreshSeconds * 1000);
  const onVisible = () => {
    if (element.isConnected) markRead();
    else document.removeEventListener("visibilitychange", onVisible);
  };
  document.addEventListener("visibilitychange", onVisible);

  /** Runs a call to the server, shows its error, and draws the answer. */
  async function run(work: () => Promise<Message | void>): Promise<void> {
    if (!bot) return;
    error.textContent = note.textContent = "";
    sendButton.disabled = attach.disabled = true;
    try {
      const message = await work();
      if (message) {
        app.chats.receive(bot.id, message);
        input.value = "";
        following = true;
        draw();
      }
    } catch (failure) {
      error.textContent = t(errorText(failure));
    } finally {
      sendButton.disabled = attach.disabled = false;
    }
  }

  const send = (text: string) => text.trim() === "" ? Promise.resolve() : run(() => api.send(bot!.id, text));
  const press = (messageId: number, data: string) => run(async () => { await api.press(bot!.id, messageId, data); });

  scroller.addEventListener("scroll", () => {
    following = scroller.scrollHeight - scroller.scrollTop - scroller.clientHeight < 80;
  });
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
  menuButton.addEventListener("click", () => { menu.hidden = !menu.hidden; });
  attach.addEventListener("click", () => picker.click());
  // CLI-16: the text in the input is the caption. A file over the limit is not sent.
  picker.addEventListener("change", () => {
    const file = picker.files?.[0];
    picker.value = "";
    if (!file) return;
    if (!fitsSize(file.size, app.maxFileSizeMb)) {
      error.textContent = t("fileTooBig");
      return;
    }
    void run(() => api.sendFile(bot!.id, file, input.value));
  });
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
      const found = await api.findBot(username);
      bot = found;
      title.textContent = bot.name;
      // CLI-25: say it at once, before the user writes.
      if (found.suspended) error.textContent = t("botSuspended");
      void refreshStatus();
      await app.chats.open(bot.id);
      draw();
      // CLI-15: the menu shows only when the bot has commands.
      const commands = await api.commands(bot.id);
      menu.replaceChildren(...commands.map(c => h("button", { type: "button", dir: "auto", onclick: () => {
        menu.hidden = true;
        void send(commandMessage(c.command));
      } }, `/${c.command} · ${c.description}`)));
      menuButton.hidden = commands.length === 0;
    } catch (failure) {
      error.textContent = t(errorText(failure));
    }
  })();

  const element = h("main", { class: "chat" },
    h("header", {},
      h("button", { type: "button", class: "link", onclick: () => app.go("/") }, t("back")),
      title,
      h("span", { class: "note", dir: "ltr" }, `@${username}`)),
    status,
    pinBar,
    scroller,
    error,
    note,
    keyboard,
    menu,
    start,
    form);

  return {
    element,
    onMessage: botId => { if (botId === bot?.id) draw(); },
    // CLI-13: the bot's answer to a press.
    onAnswer(botId, text, showAlert) {
      if (botId !== bot?.id) return;
      if (showAlert) alert(text);
      else note.textContent = text;
    },
  };
}

/** A file size for people: KB below one MB, MB above. */
function size(bytes: number): string {
  return bytes < 1024 * 1024 ? `${Math.ceil(bytes / 1024)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}
