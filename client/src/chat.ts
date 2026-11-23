// The chat with one bot: history, live messages, reply buttons (CLI-07), inline buttons (CLI-13),
// the pinned bar (CLI-14), the command menu (CLI-15), files (CLI-16), the read position (CLI-18),
// the bot's status (CLI-19), the "not delivered" mark (CLI-20), voice (CLI-46), paste and drop (CLI-47) and the input.
import { api, type Bot } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { chatHeader, matchingMessages } from "./chatHeader.ts";
import { h } from "./dom.ts";
import { botIconUrl } from "./icon.ts";
import {
  buttonAction, clock, commandMessage, fileView, firstFile, fitsSize, KeyboardState, ReadPosition, replyKeyboard, stamp,
  type ButtonStyle, type FileInfo, type InlineButton, type Message,
} from "./messages.ts";
import { recorderType, voiceFile } from "./ogg.ts";

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
  const menu = h("div", { class: "keyboard commands", hidden: true });
  const menuButton = h("button", { type: "button", hidden: true, "aria-label": t("commands"), title: t("commands") }, "☰");
  const picker = h("input", { type: "file", hidden: true });
  const attach = h("button", { type: "button", "aria-label": t("attach"), title: t("attach") }, "📎");
  const input = h("textarea", { rows: "1", dir: "auto", maxlength: String(app.maxTextLength), placeholder: t("message"), "aria-label": t("message") });
  const keyToggle = h("button", { type: "button", class: "key-toggle", hidden: true, "aria-label": t("keyboardToggle"), title: t("keyboardToggle") }, "⌨");
  const sendButton = h("button", { type: "submit", class: "primary" }, t("send"));
  const mic = h("button", { type: "button", class: "primary mic", hidden: true, "aria-label": t("record"), title: t("record") }, "🎤");
  const start = h("button", { type: "button", class: "primary start", hidden: true }, t("start"));
  const form = h("form", { class: "row compose", hidden: true }, menuButton, attach, picker, input, keyToggle, sendButton, mic);
  // CLI-47: a file waits here, with the input as its caption, until Send or Cancel.
  const pending = h("div", { class: "pending", hidden: true });
  // CLI-46: the bar while recording.
  const recTime = h("span", { class: "rec-time", role: "timer" }, "0:00");
  const recCancel = h("button", { type: "button" }, t("cancel"));
  const recSend = h("button", { type: "button", class: "primary", "aria-label": t("stopRecord"), title: t("stopRecord") }, "➤");
  const recBar = h("div", { class: "row recording", hidden: true }, h("span", { class: "rec-dot", "aria-hidden": "true" }), recTime, recCancel, recSend);
  let bot: Bot | null = null;
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
      case "image": return h("img", { src, alt: file.name, onload: () => { if (following) toEnd(); } });
      case "player": return player(src, file);
      case "video": return h("video", { src, controls: true, preload: "metadata", onloadedmetadata: () => { if (following) toEnd(); } });
      default: return h("a", { href: src, download: file.name, dir: "auto" }, `📄 ${file.name} (${size(file.size)})`);
    }
  }

  /** CLI-13: a button's colour class; none for no style. */
  const styled = (base: string, style: ButtonStyle | null | undefined) => style ? `${base} style-${style}` : base;

  function inlineButton(message: Message, button: InlineButton): HTMLElement {
    const action = buttonAction(button);
    // A link opens in a new tab and does not tell the bot's site where it came from. It has Telegram's ↗ mark.
    if ("open" in action) {
      return h("a", { class: styled("button", button.style), href: action.open, target: "_blank", rel: "noopener noreferrer", dir: "auto" },
        button.text, h("span", { class: "link-mark", "aria-hidden": "true" }, "↗"));
    }
    return h("button", { type: "button", class: styled("", button.style), dir: "auto", onclick: () => void press(message.id, action.press) }, button.text);
  }

  function bubble(message: Message): HTMLElement {
    const time = stamp(message, app.language.time(message.date), { edited: t("edited"), notDelivered: t("notDelivered") });
    return h("li", { class: message.from, "data-id": String(message.id) },
      ...(message.file ? [fileElement(message.file)] : []),
      ...(message.text ? [h("p", { dir: "auto" }, message.text)] : []),
      ...(message.inline_keyboard ?? []).map(row => h("div", { class: "row inline" }, ...row.map(button => inlineButton(message, button)))),
      h("time", { datetime: message.date }, time));
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

  /** CLI-46: the 🎤 button takes Send's place while there is nothing to send. */
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

  /** CLI-19: "online" or "last seen ...". A failed call leaves the line as it was. */
  async function refreshStatus(): Promise<void> {
    if (!bot) return;
    try {
      const seen = app.language.seen(await api.status(bot.id), new Date());
      header.status.textContent = seen ? ` · ${seen}` : "";
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
    sendButton.disabled = attach.disabled = mic.disabled = true;
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
      sendButton.disabled = attach.disabled = mic.disabled = false;
      drawSendButton();
    }
  }

  const send = (text: string) => text.trim() === "" ? Promise.resolve() : run(() => api.send(bot!.id, text));
  const press = (messageId: number, data: string) => run(async () => { await api.press(bot!.id, messageId, data); });

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
      dropFile();
      void run(() => api.sendFile(bot!.id, file, caption));
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
    if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
      event.preventDefault();
      submit();
    }
  });
  input.addEventListener("input", drawSendButton);
  // CLI-47: a pasted file goes to the card; pasted text stays text.
  input.addEventListener("paste", event => {
    const file = firstFile(event.clipboardData?.files ?? [], (f, name) => new File([f], name, { type: f.type }));
    if (!file) return;
    event.preventDefault();
    attachFile(file);
  });
  start.addEventListener("click", () => void send("/start"));
  // CLI-14: the bar takes the user to the pinned message, loading older pages when it is not here yet.
  pinBar.addEventListener("click", async () => {
    const pinned = bot ? app.chats.of(bot.id).pinned : null;
    if (!bot || !pinned) return;
    header.clearSearch();
    error.textContent = note.textContent = "";
    try {
      const found = await app.chats.find(bot.id, pinned.id);
      draw(true);
      const item = found ? list.querySelector<HTMLElement>(`li[data-id="${pinned.id}"]`) : null;
      if (!item) {
        note.textContent = t("pinnedGone");
        return;
      }
      following = false;
      item.scrollIntoView({ block: "center", behavior: "smooth" });
      item.classList.add("flash");
      window.setTimeout(() => item.classList.remove("flash"), 1200);
    } catch (failure) {
      error.textContent = t(errorText(failure));
    }
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

  // CLI-46: tap 🎤 to record, ➤ to stop and send, Cancel to drop it. Leaving the chat drops it too.
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
    void run(() => api.sendVoice(bot!.id, new Blob([voice.bytes as BlobPart], { type: voice.type }), voice.name, seconds));
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

  // CLI-07: the reply keyboard sits under the input, where a phone keyboard would be.
  const element = h("main", { class: "chat" },
    header.element,
    pinBar,
    scroller,
    error,
    note,
    menu,
    pending,
    start,
    form,
    recBar,
    keyboard);

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
