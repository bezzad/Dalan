// The chat with one bot: history, live messages, reply buttons (CLI-07), inline buttons (CLI-13),
// the pinned bar (CLI-14), the command menu (CLI-15), files (CLI-16), the read position (CLI-18),
// the bot's status (CLI-19), the "not delivered" mark (CLI-20), voice (CLI-46), paste and drop (CLI-47), replies (CLI-52),
// presses in progress (CLI-53), formatted text (CLI-54), command suggestions (CLI-74), the offline bar (CLI-91) and the input:
// Enter like Telegram (CLI-85), the growing box (CLI-86), the big editor (CLI-87), a draft per chat (CLI-88),
// the picture viewer (CLI-89) and HTML files in the app (CLI-90).
import { api, type Bot, type Command } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { chatHeader, matchingMessages, micButton, offlineBar } from "./chatHeader.ts";
import { Drafts, enterAction, expandShown, grow, insertText, pressOf } from "./compose.ts";
import { h } from "./dom.ts";
import { openEditor } from "./editor.ts";
import { formatNodes, formatTree } from "./format.ts";
import { botIconUrl } from "./icon.ts";
import { toast } from "./toast.ts";
import {
  buttonAction, clock, commandMessage, fileView, fillText, firstFile, fitsSize, isReplySwipe, KeyboardState, PressState, quoteLine, suggest,
  ReadPosition, replyKeyboard, stamp,
  type ButtonStyle, type FileInfo, type InlineButton, type Message, type ReplyInfo,
} from "./messages.ts";
import { recorderType, voiceFile } from "./ogg.ts";
import { icon } from "./public.ts";
import { openHtml, openPicture } from "./viewer.ts";

/** CLI-46: recording needs a secure page and a browser that records. */
const canRecord = () => window.isSecureContext && "MediaRecorder" in window && !!navigator.mediaDevices?.getUserMedia;

/** `tools`: the theme button, the user menu and the Dalan mark, for the end of the header (CLI-45). */
export function chatScreen(app: App, username: string, tools: HTMLElement[]): Screen {
  const t = app.language.t.bind(app.language);
  /** CLI-45: the words of the search; empty shows every message. */
  let query = "";
  const header = chatHeader({ username, t, tools, onSearch: words => { query = words; draw(); } });
  const read = new ReadPosition();
  const keys = new KeyboardState();
  const pinBar = h("button", { type: "button", class: "pinned", dir: "auto", hidden: true });
  const older = h("button", { type: "button", class: "link", hidden: true }, t("older"));
  const list = h("ol", { class: "messages" });
  const scroller = h("div", { class: "scroller" }, older, list);
  const keyboard = h("div", { class: "keyboard" });
  const error = h("p", { class: "error", role: "alert" });
  const note = h("p", { class: "note", role: "status" });
  const offline = offlineBar(username, t("botOffline"));
  const menu = h("div", { class: "keyboard commands", hidden: true });
  // CLI-74: the commands that match what the user types after "/", above the input.
  const suggestions = h("div", { class: "suggest", role: "listbox", dir: "ltr", hidden: true });
  let allCommands: Command[] = [];
  let shown: Command[] = [];
  let mark = 0;
  const menuButton = h("button", { type: "button", hidden: true, "aria-label": t("commands"), title: t("commands") }, "☰");
  const picker = h("input", { type: "file", hidden: true });
  const attach = h("button", { type: "button", "aria-label": t("attach"), title: t("attach") }, "📎");
  const input = h("textarea", { rows: "1", dir: "auto", maxlength: String(app.maxTextLength), placeholder: t("message"), "aria-label": t("message") });
  const keyToggle = h("button", { type: "button", class: "key-toggle", hidden: true, "aria-label": t("keyboardToggle"), title: t("keyboardToggle") }, "⌨");
  const sendButton = h("button", { type: "submit", class: "primary" }, t("send"));
  const mic = micButton(t("record"));
  const start = h("button", { type: "button", class: "primary start", hidden: true }, t("start"));
  // CLI-87: over 3 lines, the expand button in the corner of the box opens the big editor.
  const expand = h("button", { type: "button", class: "expand", hidden: true, "aria-label": t("expand"), title: t("expand") }, icon("expand"));
  const box = h("div", { class: "box" }, input, expand);
  const form = h("form", { class: "row compose", hidden: true }, menuButton, attach, picker, box, keyToggle, sendButton, mic);
  // CLI-88: the unsent text of each chat, in this browser only.
  const drafts = new Drafts(localStorage, app.user.id);
  let draftTimer = 0;
  // CLI-47: a file waits here, with the input as its caption, until Send or Cancel.
  const pending = h("div", { class: "pending", hidden: true });
  // CLI-46: the bar while recording.
  const recTime = h("span", { class: "rec-time", role: "timer" }, "0:00");
  const recCancel = h("button", { type: "button" }, t("cancel"));
  const recSend = h("button", { type: "button", class: "primary", "aria-label": t("stopRecord"), title: t("stopRecord") }, "➤");
  const recBar = h("div", { class: "row recording", hidden: true }, h("span", { class: "rec-dot", "aria-hidden": "true" }), recTime, recCancel, recSend);
  // CLI-52: the message the next send replies to, shown in a bar above the input.
  const replyBar = h("div", { class: "reply-bar", hidden: true });
  // CLI-52: Reply and Copy for one message, from its menu (right-click or a long press).
  const messageMenu = h("div", { class: "message-menu", role: "menu", hidden: true });
  const presses = new PressState();
  let replyTarget: Message | null = null;
  let bot: Bot | null = null;
  /** CLI-25: a suspended bot shows its own error, not the offline bar (CLI-91). */
  let suspended = false;
  let waitingFile: File | null = null;
  /** True while the newest message is in view; then new messages and loaded pictures keep it in view. */
  let following = true;
  const toEnd = () => { scroller.scrollTop = scroller.scrollHeight; };

  /** CLI-16: Dalan's own player: play and pause, a seek bar, the time and the length. */
  function player(src: string, file: FileInfo): HTMLElement {
    const audio = h("audio", { src, preload: "none" });
    const length = file.duration ?? 0;
    const button = h("button", { type: "button", class: "play", "aria-label": t("play"), title: t("play") }, "▶");
    const bar = h("input", { type: "range", min: "0", max: String(length || 1), step: "0.1", value: "0", "aria-label": file.name });
    const time = h("span", { class: "time" }, clock(length));
    const known = () => (Number.isFinite(audio.duration) && audio.duration > 0 ? audio.duration : length);
    button.addEventListener("click", () => { if (audio.paused) void audio.play(); else audio.pause(); });
    audio.addEventListener("play", () => { button.textContent = "⏸"; button.setAttribute("aria-label", t("pause")); });
    const paused = () => { button.textContent = "▶"; button.setAttribute("aria-label", t("play")); };
    audio.addEventListener("pause", paused);
    audio.addEventListener("ended", () => { paused(); time.textContent = clock(known()); });
    audio.addEventListener("loadedmetadata", () => { bar.max = String(known() || 1); });
    audio.addEventListener("timeupdate", () => {
      bar.value = String(audio.currentTime);
      time.textContent = clock(audio.currentTime);
    });
    bar.addEventListener("input", () => { audio.currentTime = Number(bar.value); });
    // Time runs left to right, in Persian too, as in Telegram.
    return h("div", { class: `player ${file.kind}`, dir: "ltr" }, button, bar, time, audio);
  }

  function fileElement(file: FileInfo): HTMLElement {
    const src = api.fileUrl(bot!.id, file.id);
    switch (fileView(file)) {
      // CLI-89: a tap opens the picture full screen.
      case "image": return h("img", { src, alt: file.name, tabindex: "0", role: "button", class: "picture",
        onload: () => { if (following) toEnd(); }, onclick: () => openPicture(t, src, file.name),
        onkeydown: (event: Event) => { if ((event as KeyboardEvent).key === "Enter") openPicture(t, src, file.name); } });
      case "html": return htmlCard(file, src);
      case "player": return player(src, file);
      case "video": return h("video", { src, controls: true, preload: "metadata", onloadedmetadata: () => { if (following) toEnd(); } });
      default: return h("a", { href: src, download: file.name, dir: "auto" }, `📄 ${file.name} (${size(file.size)})`);
    }
  }

  /** CLI-90: an HTML file is a card with its name, size, Open (when the viewer is on) and Download. */
  function htmlCard(file: FileInfo, src: string): HTMLElement {
    const open = h("button", { type: "button", class: "primary" }, t("open"));
    open.addEventListener("click", async () => {
      open.disabled = true;
      try {
        const link = await api.fileViewLink(bot!.id, file.id);
        openHtml(t, link.url, src, file.name);
      } catch (failure) {
        toast(t(errorText(failure)), "error");
      } finally {
        open.disabled = false;
      }
    });
    return h("div", { class: "file-card" },
      h("span", { class: "file-name", dir: "auto" }, `📄 ${file.name}`),
      h("span", { class: "note", dir: "ltr" }, size(file.size)),
      h("div", { class: "row" }, ...(app.fileView ? [open] : []), h("a", { class: "button", href: src, download: file.name }, t("download"))));
  }

  /** CLI-13: a button's colour class; none for no style. */
  const styled = (base: string, style: ButtonStyle | null | undefined) => style ? `${base} style-${style}` : base;

  /** `index`: the button's place in the message; `waiting`: the index of the button in progress (CLI-53), or null. */
  function inlineButton(message: Message, button: InlineButton, index: number, waiting: number | null): HTMLElement {
    const action = buttonAction(button);
    // A link opens in a new tab and does not tell the bot's site where it came from. It has Telegram's ↗ mark.
    if ("open" in action) {
      return h("a", { class: styled("button", button.style), href: action.open, target: "_blank", rel: "noopener noreferrer", dir: "auto" },
        button.text, h("span", { class: "link-mark", "aria-hidden": "true" }, "↗"));
    }
    // CLI-13: a switch button puts "@bot query" in the input, as Telegram does in the current chat.
    if ("fill" in action) {
      return h("button", { type: "button", class: styled("", button.style), dir: "auto", onclick: () => fill(action.fill) }, button.text);
    }
    // CLI-53: while a press waits for the bot, every button of the message is locked and the pressed one moves.
    return h("button", {
      type: "button", class: styled(waiting === index ? "waiting" : "", button.style), dir: "auto", disabled: waiting !== null,
      "aria-busy": waiting === index ? "true" : "false", title: waiting === index ? t("pressWait") : false,
      onclick: () => void press(message, index, action.press),
    }, button.text);
  }

  /** CLI-52: who wrote a quoted message. */
  const writer = (from: "user" | "bot") => from === "user" ? t("you") : bot?.name ?? "";

  /** CLI-52: the quote at the top of a message that replies; a click goes to the message. */
  function quote(reply: ReplyInfo): HTMLElement {
    const target = bot ? app.chats.of(bot.id).messages.find(m => m.id === reply.id) : undefined;
    const line = target?.deleted ? t("deletedMessage") : quoteLine(reply);
    return h("button", { type: "button", class: "quote", dir: "auto", onclick: () => void goTo(reply.id, "replyGone") },
      h("strong", {}, writer(reply.from)), h("span", {}, line));
  }

  function bubble(message: Message): HTMLElement {
    const time = stamp(message, app.language.time(message.date), { edited: t("edited"), notDelivered: t("notDelivered") });
    const waiting = presses.pending(message, Date.now());
    let index = 0;
    const item = h("li", { class: message.from, "data-id": String(message.id) },
      ...(message.reply_to ? [quote(message.reply_to)] : []),
      ...(message.file ? [fileElement(message.file)] : []),
      ...(message.text ? [h("p", { dir: "auto" }, ...formatNodes(formatTree(message.text, message.entities)))] : []),
      ...(message.inline_keyboard ?? []).map(row => h("div", { class: "row inline" },
        ...row.map(button => inlineButton(message, button, index++, waiting)))),
      h("time", { datetime: message.date }, time));
    // CLI-52: Reply from the message's menu, a double-click, or a swipe to the left.
    item.addEventListener("contextmenu", event => {
      if ((event.target as Element).closest("a, button, input, audio, video")) return;
      event.preventDefault();
      openMenu(message, event.clientX, event.clientY);
    });
    item.addEventListener("dblclick", event => {
      if ((event.target as Element).closest("a, button, input")) return;
      window.getSelection()?.removeAllRanges();
      startReply(message);
    });
    let touch: { x: number; y: number } | null = null;
    item.addEventListener("touchstart", event => {
      const first = event.touches[0];
      touch = event.touches.length === 1 && first ? { x: first.clientX, y: first.clientY } : null;
    }, { passive: true });
    item.addEventListener("touchend", event => {
      const last = event.changedTouches[0];
      if (touch && last && isReplySwipe(last.clientX - touch.x, last.clientY - touch.y)) startReply(message);
      touch = null;
    });
    return item;
  }

  /** CLI-52: the small menu of one message. */
  function openMenu(message: Message, x: number, y: number): void {
    const close = () => { messageMenu.hidden = true; };
    const item = (label: string, action: () => void) =>
      h("button", { type: "button", role: "menuitem", onclick: () => { close(); action(); } }, label);
    messageMenu.replaceChildren(
      item(t("reply"), () => startReply(message)),
      ...(message.text ? [item(t("copy"), () => void navigator.clipboard.writeText(message.text)
        .then(() => toast(t("copied")), () => toast(t("failed"), "error")))] : []));
    messageMenu.style.left = `${Math.min(x, window.innerWidth - 180)}px`;
    messageMenu.style.top = `${Math.min(y, window.innerHeight - 100)}px`;
    messageMenu.hidden = false;
    messageMenu.querySelector("button")?.focus();
    // Any click or key elsewhere closes it.
    const away = (event: Event) => {
      if (event instanceof KeyboardEvent && event.key !== "Escape") return;
      if (event.target instanceof Node && messageMenu.contains(event.target) && event.type !== "keydown") return;
      close();
      document.removeEventListener("pointerdown", away, true);
      document.removeEventListener("keydown", away, true);
      scroller.removeEventListener("scroll", away);
    };
    document.addEventListener("pointerdown", away, true);
    document.addEventListener("keydown", away, true);
    scroller.addEventListener("scroll", away);
  }

  /** CLI-52: the next message replies to this one. */
  function startReply(message: Message): void {
    if (message.deleted || form.hidden) return;
    replyTarget = message;
    const reply: ReplyInfo = { id: message.id, from: message.from, text: message.text, ...(message.file ? { file: message.file } : {}) };
    replyBar.replaceChildren(
      h("span", { class: "reply-mark", "aria-hidden": "true" }, "↩"),
      h("button", { type: "button", class: "quote", dir: "auto", onclick: () => void goTo(message.id, "replyGone") },
        h("strong", {}, writer(message.from)), h("span", {}, quoteLine(reply))),
      h("button", { type: "button", class: "close", "aria-label": t("cancelReply"), title: t("cancelReply"), onclick: cancelReply }, "✕"));
    replyBar.hidden = false;
    input.focus();
  }

  function cancelReply(): void {
    replyTarget = null;
    replyBar.hidden = true;
    replyBar.replaceChildren();
  }

  /** CLI-13: a switch button's text goes in the input. */
  function fill(query: string): void {
    if (!bot || form.hidden) return;
    input.value = fillText(bot.username, query);
    drawSendButton();
    input.focus();
    input.setSelectionRange(input.value.length, input.value.length);
  }

  /** CLI-07: the reply keyboard under the input, each row as the bot sent it. */
  function drawKeyboard(messages: Message[]): void {
    const current = replyKeyboard(messages);
    const shown = keys.visible(current);
    keyToggle.hidden = !keys.hasToggle(current);
    keyToggle.setAttribute("aria-pressed", String(shown));
    keyboard.classList.toggle("fixed", !!current && !current.resize);
    keyboard.replaceChildren(...(current && shown ? current.rows.map(row =>
      h("div", { class: "row keys" }, ...row.map(button => h("button", {
        type: "button", class: styled("", button.style), dir: "auto", onclick: () => {
          keys.tapped(current);
          void send(button.text);
        },
      }, button.text)))) : []));
  }

  function draw(keepPlace = false): void {
    if (!bot) return;
    const chat = app.chats.of(bot.id);
    const messages = chat.visible;
    const heightBefore = scroller.scrollHeight;

    pinBar.hidden = !chat.pinned;
    pinBar.textContent = chat.pinned ? `📌 ${chat.pinned.text || chat.pinned.file?.name || ""}` : "";
    const shown = matchingMessages(messages, query);
    list.replaceChildren(...(shown.length === 0 && query.trim() !== "" ? [h("li", { class: "note" }, t("noMatches"))] : shown.map(bubble)));
    older.hidden = chat.reachedStart || messages.length === 0;
    drawKeyboard(messages);

    const started = chat.messages.length > 0;
    start.hidden = started;
    form.hidden = !started || recording !== null;
    drawSendButton();

    // Older messages were added on top: stay at the same message. Otherwise follow the newest one.
    if (keepPlace) scroller.scrollTop += scroller.scrollHeight - heightBefore;
    else if (following) toEnd();
    markRead();
  }

  /** CLI-46: the microphone button takes Send's place while there is nothing to send. */
  function drawSendButton(): void {
    const empty = input.value.trim() === "" && waitingFile === null;
    mic.hidden = !(empty && canRecord());
    sendButton.hidden = !mic.hidden;
  }

  /** CLI-18: tells the server how far the user has read, while the page is visible. */
  function markRead(): void {
    const newest = bot ? app.chats.of(bot.id).messages.at(-1)?.id : undefined;
    if (newest === undefined) return;
    const id = read.next(newest, document.visibilityState === "visible");
    if (id !== null) void api.read(bot!.id, id).catch(() => {});
  }

  /** CLI-19: "online" or "last seen ..."; CLI-91: the offline bar. A failed call leaves both as they were. */
  async function refreshStatus(): Promise<void> {
    if (!bot) return;
    try {
      const status = await api.status(bot.id);
      const seen = app.language.seen(status, new Date());
      header.status.textContent = seen ? ` · ${seen}` : "";
      offline.update(status, suspended);
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

  /** Runs a call to the server, shows its error, and draws the answer. A sent message ends the reply (CLI-52). */
  async function run(work: () => Promise<Message | void>): Promise<void> {
    if (!bot) return;
    error.textContent = note.textContent = "";
    sendButton.disabled = attach.disabled = mic.disabled = true;
    try {
      const message = await work();
      if (message) {
        app.chats.receive(bot.id, message);
        clearBox();
        // Only the reply that was sent ends; one the user picked while it was on its way stays.
        if (message.reply_to && message.reply_to.id === replyTarget?.id) cancelReply();
        following = true;
        draw();
      }
    } catch (failure) {
      error.textContent = t(errorText(failure));
    } finally {
      sendButton.disabled = attach.disabled = mic.disabled = false;
      drawSendButton();
      drawSuggest();
    }
  }

  /** CLI-52: the id the next send replies to, if any. */
  const replyId = () => replyTarget?.id;
  /** CLI-86 and CLI-87: fits the box to its text and shows the expand button over 3 lines. */
  function resize(): void {
    expand.hidden = !expandShown(grow(input));
  }
  /** CLI-86 and CLI-88: after a send the box is empty, one row high, and the chat has no draft. */
  function clearBox(): void {
    input.value = "";
    window.clearTimeout(draftTimer);
    if (bot) drafts.remove(bot.id);
    resize();
  }
  /** CLI-74: draws the list for the input now; it closes when nothing matches. */
  function drawSuggest(keepMark = false): void {
    shown = suggest(allCommands, input.value);
    mark = keepMark ? Math.min(mark, shown.length - 1) : 0;
    suggestions.hidden = shown.length === 0;
    suggestions.replaceChildren(...shown.map((c, i) => h("button", {
      type: "button", role: "option", class: i === mark ? "marked" : "", "aria-selected": String(i === mark),
      onmousedown: (event: Event) => event.preventDefault(),
      onclick: () => { clearBox(); drawSuggest(); void send(commandMessage(c.command)); },
    }, h("b", {}, `/${c.command}`), h("span", { dir: "auto" }, c.description))));
    suggestions.children[mark]?.scrollIntoView?.({ block: "nearest" });
  }
  const send = (text: string) => {
    const replyTo = replyId();
    return text.trim() === "" ? Promise.resolve() : run(() => api.send(bot!.id, text, replyTo));
  };

  /** CLI-53: the press waits, with the buttons of its message locked, until the bot answers, changes the message, or the time is over. */
  async function press(message: Message, index: number, data: string): Promise<void> {
    if (!bot || presses.pending(message, Date.now()) !== null) return;
    error.textContent = note.textContent = "";
    const token = presses.start(message, index, Date.now(), app.pressWaitSeconds);
    draw();
    window.setTimeout(() => { if (element.isConnected) draw(); }, app.pressWaitSeconds * 1000 + 50);
    try {
      // A fast bot may have answered before this call came back: then the wait ends here.
      if (presses.sent(message.id, token, (await api.press(bot.id, message.id, data)).id)) draw();
    } catch (failure) {
      presses.end(message.id, token);
      error.textContent = t(errorText(failure));
      draw();
    }
  }

  /** CLI-14 and CLI-52: goes to a message, loading older pages when it is not here yet. */
  async function goTo(id: number, gone: "pinnedGone" | "replyGone"): Promise<void> {
    if (!bot) return;
    header.clearSearch();
    error.textContent = note.textContent = "";
    try {
      const found = await app.chats.find(bot.id, id);
      draw(true);
      const item = found ? list.querySelector<HTMLElement>(`li[data-id="${id}"]`) : null;
      if (!item) {
        note.textContent = t(gone);
        return;
      }
      following = false;
      item.scrollIntoView({ block: "center", behavior: "smooth" });
      item.classList.add("flash");
      window.setTimeout(() => item.classList.remove("flash"), 1200);
    } catch (failure) {
      error.textContent = t(errorText(failure));
    }
  }

  // CLI-16 and CLI-47: one card for 📎, paste and drop. A file over the limit is not taken.
  function attachFile(file: File | null): void {
    if (!file || !bot) return;
    if (!fitsSize(file.size, app.maxFileSizeMb)) {
      error.textContent = t("fileTooBig");
      return;
    }
    error.textContent = "";
    waitingFile = file;
    const preview = file.type.startsWith("image/")
      ? h("img", { src: URL.createObjectURL(file), alt: file.name })
      : h("span", { class: "file-name", dir: "auto" }, `📄 ${file.name} (${size(file.size)})`);
    pending.replaceChildren(preview, h("span", { class: "note" }, t("caption")),
      h("button", { type: "button", class: "small", onclick: dropFile }, t("cancel")));
    pending.hidden = false;
    input.placeholder = t("caption");
    drawSendButton();
    input.focus();
  }

  function dropFile(): void {
    const image = pending.querySelector("img");
    if (image) URL.revokeObjectURL(image.src);
    waitingFile = null;
    pending.hidden = true;
    pending.replaceChildren();
    input.placeholder = t("message");
    drawSendButton();
  }

  function submit(): void {
    const file = waitingFile;
    if (file) {
      const caption = input.value;
      const replyTo = replyId();
      dropFile();
      void run(() => api.sendFile(bot!.id, file, caption, replyTo));
    } else void send(input.value);
  }

  scroller.addEventListener("scroll", () => {
    following = scroller.scrollHeight - scroller.scrollTop - scroller.clientHeight < 80;
  });
  form.addEventListener("submit", event => {
    event.preventDefault();
    submit();
  });
  input.addEventListener("keydown", event => {
    if (!suggestions.hidden && !event.isComposing) {
      const step = event.key === "ArrowDown" ? 1 : event.key === "ArrowUp" ? -1 : 0;
      if (step) {
        event.preventDefault();
        mark = (mark + step + shown.length) % shown.length;
        drawSuggest(true);
        return;
      }
      if (event.key === "Escape") { event.preventDefault(); suggestions.hidden = true; return; }
      if (event.key === "Tab" && !event.shiftKey) {
        event.preventDefault();
        input.value = `${commandMessage(shown[mark]!.command)} `;
        drawSuggest();
        return;
      }
      if (event.key === "Enter" && !event.shiftKey) {
        event.preventDefault();
        const chosen = shown[mark];
        clearBox();
        drawSuggest();
        void send(commandMessage(chosen!.command));
        return;
      }
    }
    // CLI-85: a fine pointer sends on Enter; a coarse one adds a line. Shift+Enter keeps the browser's own new line.
    const action = enterAction(pressOf(event));
    if (action === "send") {
      event.preventDefault();
      submit();
    } else if (action === "newline" && !event.shiftKey) {
      event.preventDefault();
      insertText(input, "\n");
    }
    // CLI-52: Esc drops the reply.
    if (event.key === "Escape" && replyTarget) {
      event.preventDefault();
      cancelReply();
    }
  });
  input.addEventListener("input", () => {
    drawSendButton();
    drawSuggest();
    resize();
    // CLI-88: kept after a short quiet, not at every key.
    window.clearTimeout(draftTimer);
    const botId = bot?.id;
    // Not after the chat is gone: a logout in those 300 ms has already removed every draft.
    if (botId !== undefined) draftTimer = window.setTimeout(() => { if (element.isConnected) drafts.save(botId, input.value); }, 300);
  });
  expand.addEventListener("click", () => openEditor(t, input, submit));
  // CLI-47: a pasted file goes to the card; pasted text stays text.
  input.addEventListener("paste", event => {
    const file = firstFile(event.clipboardData?.files ?? [], (f, name) => new File([f], name, { type: f.type }));
    if (!file) return;
    event.preventDefault();
    attachFile(file);
  });
  start.addEventListener("click", () => void send("/start"));
  // CLI-14: the bar takes the user to the pinned message, loading older pages when it is not here yet.
  pinBar.addEventListener("click", () => {
    const pinned = bot ? app.chats.of(bot.id).pinned : null;
    if (pinned) void goTo(pinned.id, "pinnedGone");
  });
  menuButton.addEventListener("click", () => { menu.hidden = !menu.hidden; });
  keyToggle.addEventListener("click", () => {
    const current = bot ? replyKeyboard(app.chats.of(bot.id).visible) : null;
    if (current) keys.toggle(current);
    draw();
  });
  attach.addEventListener("click", () => picker.click());
  picker.addEventListener("change", () => {
    const file = picker.files?.[0] ?? null;
    picker.value = "";
    attachFile(file);
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

  // CLI-46: tap the microphone button to record, ➤ to stop and send, Cancel to drop it. Leaving the chat drops it too.
  interface Recording {
    recorder: MediaRecorder; stream: MediaStream; chunks: Blob[]; bytes: number; started: number; timer: number; send: boolean;
    /** Set by the first stop: a second tap, or Cancel after Send, changes nothing. */
    stopping: boolean;
    finished: boolean;
  }
  let recording: Recording | null = null;
  /** True while the browser asks for the microphone: a second tap must not open a second one. */
  let starting = false;

  function stopRecording(sendIt: boolean): void {
    const current = recording;
    if (!current || current.stopping) return;
    current.stopping = true;
    current.send = sendIt;
    window.clearInterval(current.timer);
    if (current.recorder.state !== "inactive") current.recorder.stop();
    else void finish(current);
  }

  async function finish(done: Recording): Promise<void> {
    if (done.finished) return;
    done.finished = true;
    done.stream.getTracks().forEach(track => track.stop());
    recording = null;
    recBar.hidden = true;
    draw();
    if (!done.send || !bot) return;
    const seconds = Math.max(1, Math.round((performance.now() - done.started) / 1000));
    const recorded = new Uint8Array(await new Blob(done.chunks).arrayBuffer());
    const voice = voiceFile(recorded, done.recorder.mimeType || done.chunks[0]?.type || "");
    const replyTo = replyId();
    void run(() => api.sendVoice(bot!.id, new Blob([voice.bytes as BlobPart], { type: voice.type }), voice.name, seconds, replyTo));
  }

  mic.addEventListener("click", async () => {
    if (recording || starting || !bot) return;
    error.textContent = "";
    let stream: MediaStream;
    starting = true;
    try {
      stream = await navigator.mediaDevices.getUserMedia({ audio: true });
    } catch {
      error.textContent = t("micDenied");
      return;
    } finally {
      starting = false;
    }
    // The chat was left while the browser asked: give the microphone back at once.
    if (!element.isConnected) return stream.getTracks().forEach(track => track.stop());
    const type = recorderType(asked => MediaRecorder.isTypeSupported(asked));
    let recorder: MediaRecorder;
    try {
      recorder = new MediaRecorder(stream, type ? { mimeType: type } : {});
    } catch {
      stream.getTracks().forEach(track => track.stop());
      error.textContent = t("failed");
      return;
    }
    const limit = app.maxFileSizeMb * 1024 * 1024 * 0.95;
    const current: Recording = {
      recorder, stream, chunks: [], bytes: 0, started: performance.now(), timer: 0, send: false, stopping: false, finished: false,
    };
    // A recorder that fails keeps nothing and gives the microphone back.
    recorder.addEventListener("error", () => {
      current.send = false;
      current.stopping = true;
      window.clearInterval(current.timer);
      void finish(current);
    });
    recorder.addEventListener("dataavailable", event => {
      current.chunks.push(event.data);
      current.bytes += event.data.size;
      // A voice that reaches the file size limit is sent as it is.
      if (current.bytes >= limit) stopRecording(true);
    });
    recorder.addEventListener("stop", () => void finish(current));
    current.timer = window.setInterval(() => {
      if (!element.isConnected) return stopRecording(false);
      recTime.textContent = clock((performance.now() - current.started) / 1000);
    }, 250);
    recording = current;
    recTime.textContent = "0:00";
    recBar.hidden = false;
    form.hidden = true;
    try {
      recorder.start(1000);
    } catch {
      current.stopping = true;
      window.clearInterval(current.timer);
      void finish(current);
      error.textContent = t("failed");
    }
  });
  recCancel.addEventListener("click", () => stopRecording(false));
  recSend.addEventListener("click", () => stopRecording(true));

  (async () => {
    try {
      const found = await api.findBot(username);
      bot = found;
      header.showBot(bot.name, botIconUrl(bot.id, bot.icon_version));
      // CLI-25: say it at once, before the user writes.
      suspended = found.suspended;
      if (suspended) error.textContent = t("botSuspended");
      void refreshStatus();
      // CLI-88: the text this chat had when the user left it.
      // What the user typed while the chat was loading wins.
      if (input.value === "") input.value = drafts.load(bot.id);
      await app.chats.open(bot.id);
      draw();
      resize();
      drawSendButton();
      // CLI-15: the menu shows only when the bot has commands.
      const commands = await api.commands(bot.id);
      menu.replaceChildren(...commands.map(c => h("button", { type: "button", dir: "auto", onclick: () => {
        menu.hidden = true;
        void send(commandMessage(c.command));
      } }, `/${c.command} · ${c.description}`)));
      menuButton.hidden = commands.length === 0;
      allCommands = commands;
    } catch (failure) {
      error.textContent = t(errorText(failure));
    }
  })();

  // CLI-07: the reply keyboard sits under the input, where a phone keyboard would be.
  const element = h("main", { class: "chat" },
    header.element,
    pinBar,
    scroller,
    error,
    note,
    offline.element,
    menu,
    suggestions,
    pending,
    replyBar,
    start,
    form,
    recBar,
    keyboard,
    messageMenu);

  // CLI-47: a file dropped anywhere on the chat.
  element.addEventListener("dragover", event => {
    if (event.dataTransfer?.types.includes("Files")) event.preventDefault();
  });
  element.addEventListener("drop", event => {
    const file = firstFile(event.dataTransfer?.files ?? [], (f, name) => new File([f], name, { type: f.type }));
    if (!file) return;
    event.preventDefault();
    attachFile(file);
  });

  return {
    element,
    onMessage: botId => { if (botId === bot?.id) draw(); },
    // CLI-13 and CLI-53: the bot's answer to a press ends its wait, and its text is shown.
    onAnswer(botId, answer) {
      if (botId !== bot?.id) return;
      if (presses.answered(answer.callback_id)) draw();
      if (!answer.text) return;
      if (answer.show_alert) alert(answer.text);
      else note.textContent = answer.text;
    },
  };
}

/** A file size for people: KB below one MB, MB above. */
function size(bytes: number): string {
  return bytes < 1024 * 1024 ? `${Math.ceil(bytes / 1024)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}
