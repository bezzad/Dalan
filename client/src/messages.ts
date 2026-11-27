// The messages of the chats, as pure logic with no DOM and no network (CLI-05, CLI-06, CLI-07, CLI-13 to CLI-17).

/** REL-17 and REL-20: Telegram's button colours. */
export type ButtonStyle = "success" | "danger" | "primary";

export interface InlineButton {
  text: string;
  callback_data?: string;
  url?: string;
  style?: ButtonStyle;
  /** REL-20: a tap puts "@username " and this text in the input. */
  switch_query?: string;
}

/** REL-53: one formatted part of a text; offsets and lengths count UTF-16 code units, as JavaScript strings do. */
export interface Entity {
  type: string;
  offset: number;
  length: number;
  url?: string;
  language?: string;
}

/** REL-52: a copy of the message replied to. */
export interface ReplyInfo {
  id: number;
  from: "user" | "bot";
  text: string;
  file?: FileInfo;
  entities?: Entity[];
}

/** REL-17: sent only when a flag or a style is set. `styles` has the shape of the rows. */
export interface KeyboardOptions {
  persistent?: boolean;
  resize?: boolean;
  one_time?: boolean;
  styles?: (ButtonStyle | null)[][];
}

export interface FileInfo {
  id: string;
  kind: "photo" | "document" | "audio" | "voice" | "video";
  name: string;
  /** The content type. */
  type: string;
  size: number;
  /** REL-50: the length in seconds, when known. */
  duration?: number;
}

export interface Message {
  /** The sequence number in the chat. */
  id: number;
  /** Rises with every change of the chat (REL-16); the higher copy of a message wins. */
  version: number;
  from: "user" | "bot";
  /** The text, or the caption of a file. */
  text: string;
  date: string;
  /** Rows of button texts. Missing = this message does not change the buttons. Empty = remove them. */
  reply_keyboard?: string[][];
  reply_keyboard_options?: KeyboardOptions;
  inline_keyboard?: InlineButton[][];
  file?: FileInfo;
  /** Time of the last edit. */
  edited?: string;
  pinned?: boolean;
  deleted?: boolean;
  /** REL-31: a user message the bot never got. */
  not_delivered?: boolean;
  /** REL-53 */
  entities?: Entity[];
  /** REL-52 */
  reply_to?: ReplyInfo;
}

/** One answer of the messages call: a page and the pinned message of the chat. */
export interface Page {
  messages: Message[];
  pinned: Message | null;
}

/** CLI-05: one list in rising id order, each id once; of two copies the higher version wins. Deleted stubs stay, to block old copies. */
export function merge(list: readonly Message[], incoming: readonly Message[]): Message[] {
  const byId = new Map<number, Message>();
  for (const message of [...list, ...incoming]) {
    const known = byId.get(message.id);
    if (!known || message.version >= known.version) byId.set(message.id, message);
  }
  return [...byId.values()].sort((a, b) => a.id - b.id);
}

/** CLI-07: the buttons under the input come from the newest bot message that carries a keyboard. */
export function currentKeyboard(list: readonly Message[]): string[][] {
  return replyKeyboard(list)?.rows.map(row => row.map(button => button.text)) ?? [];
}

export interface KeyButton { text: string; style: ButtonStyle | null }

/** CLI-07: the reply keyboard to show, with the id of its message; null when there is none or it was removed. */
export interface ReplyKeyboard {
  messageId: number;
  /** As the bot sent them: never joined or split. */
  rows: KeyButton[][];
  persistent: boolean;
  resize: boolean;
  oneTime: boolean;
}

export function replyKeyboard(list: readonly Message[]): ReplyKeyboard | null {
  for (let i = list.length - 1; i >= 0; i--) {
    const message = list[i]!;
    if (message.from !== "bot" || message.deleted || !message.reply_keyboard) continue;
    if (message.reply_keyboard.length === 0) return null;
    const options = message.reply_keyboard_options ?? {};
    return {
      messageId: message.id,
      rows: message.reply_keyboard.map((row, r) => row.map((text, b) => ({ text, style: options.styles?.[r]?.[b] ?? null }))),
      persistent: options.persistent === true,
      resize: options.resize === true,
      oneTime: options.one_time === true,
    };
  }
  return null;
}

/**
 * CLI-07: hidden or shown, per chat. A one-time keyboard hides after a tap; the ⌨ button hides and shows one that is
 * not persistent; a new keyboard (another message) is shown again.
 */
export class KeyboardState {
  private hiddenFor: number | null = null;

  visible(keyboard: ReplyKeyboard | null): boolean {
    return keyboard !== null && (keyboard.persistent || this.hiddenFor !== keyboard.messageId);
  }

  /** The ⌨ button is there only for a keyboard that may hide. */
  hasToggle(keyboard: ReplyKeyboard | null): boolean {
    return keyboard !== null && !keyboard.persistent;
  }

  toggle(keyboard: ReplyKeyboard): void {
    this.hiddenFor = this.visible(keyboard) ? keyboard.messageId : null;
  }

  tapped(keyboard: ReplyKeyboard): void {
    if (keyboard.oneTime && !keyboard.persistent) this.hiddenFor = keyboard.messageId;
  }
}

/** CLI-16: a length for people: 0:07, 1:05, 1:02:03. */
export function clock(seconds: number): string {
  const s = Math.max(0, Math.floor(seconds));
  const two = (value: number) => String(value).padStart(2, "0");
  return s >= 3600 ? `${Math.floor(s / 3600)}:${two(Math.floor(s / 60) % 60)}:${two(s % 60)}` : `${Math.floor(s / 60)}:${two(s % 60)}`;
}

/** CLI-47: the first file of a paste or a drop; an image with no real name is named `image.<type>`. Null for text only. */
export function firstFile(files: ArrayLike<File>, rename: (file: File, name: string) => File): File | null {
  const file = files[0];
  if (!file) return null;
  if (file.type.startsWith("image/") && (file.name === "" || file.name === "image.png" || file.name === "blob")) {
    const extension = file.type.slice("image/".length).replace("jpeg", "jpg").replace(/[^a-z0-9]/g, "") || "png";
    return rename(file, `image.${extension}`);
  }
  return file;
}

/** CLI-13: what a tap on an inline button does. */
export function buttonAction(button: InlineButton): { press: string } | { open: string } | { fill: string } {
  if (button.url !== undefined) return { open: button.url };
  if (button.switch_query !== undefined) return { fill: button.switch_query };
  return { press: button.callback_data ?? "" };
}

/** CLI-13: the input after a tap on a switch button, as in Telegram's current chat. */
export function fillText(username: string, query: string): string {
  return `@${username} ${query}`;
}

/** CLI-52: the line a quote shows: the first line of the text, else the file name. */
export function quoteLine(reply: ReplyInfo): string {
  const line = reply.text.split("\n").find(part => part.trim() !== "") ?? "";
  return line.trim() || reply.file?.name || "";
}

/** CLI-52: a swipe to the left, mostly sideways, starts a reply. */
export function isReplySwipe(dx: number, dy: number): boolean {
  return dx <= -60 && Math.abs(dy) < Math.abs(dx) / 2;
}

/**
 * CLI-53: the press each message waits for. A press ends with the bot's answer to it, a newer copy of its message,
 * a failure, or when its time is over. In memory only.
 */
export class PressState {
  private readonly waits = new Map<number, { token: number; button: number; version: number; until: number; id: string | null }>();
  /** Answers that came before the server gave the press id (a fast bot). */
  private readonly early = new Set<string>();
  private tokens = 0;

  /**
   * The user tapped button `button` (its index in the message) of this message. The token names this press, so a late
   * answer of an older press of the same message never touches a newer one.
   */
  start(message: Message, button: number, now: number, waitSeconds: number): number {
    const token = ++this.tokens;
    this.waits.set(message.id, { token, button, version: message.version, until: now + waitSeconds * 1000, id: null });
    return token;
  }

  /** The server took the press and gave it this id. True when that ended the wait (the bot had answered already). */
  sent(messageId: number, token: number, pressId: string): boolean {
    const wait = this.waits.get(messageId);
    const answered = this.early.delete(pressId);
    if (wait?.token !== token) return false;
    if (answered) return this.waits.delete(messageId);
    wait.id = pressId;
    return false;
  }

  /** The press failed. */
  end(messageId: number, token: number): void {
    if (this.waits.get(messageId)?.token === token) this.waits.delete(messageId);
  }

  /** LIVE-09: the bot answered this press. True when a wait ended. */
  answered(pressId: string): boolean {
    for (const [messageId, wait] of this.waits) {
      if (wait.id === pressId) return this.waits.delete(messageId);
    }
    if (this.waits.size > 0) this.early.add(pressId);
    return false;
  }

  /** The index of the button in progress, or null; a newer copy of the message or the end of the time ends the wait. */
  pending(message: Message, now: number): number | null {
    const wait = this.waits.get(message.id);
    if (!wait) return null;
    if (message.version > wait.version || now >= wait.until) {
      this.waits.delete(message.id);
      return null;
    }
    return wait.button;
  }
}

/** CLI-55: the manifest a page links; a bot's chat links its own, so Install there installs that bot. */
export function manifestFor(path: string): string {
  const username = /^\/@([A-Za-z0-9_]+)$/.exec(path)?.[1];
  return username ? `/@${username}/manifest.webmanifest` : "/manifest.webmanifest";
}

/** CLI-89: the picture types shown in the page (SEC-12 sends these inline). Not SVG: it can hold script. */
const pictureTypes = new Set(["image/jpeg", "image/png", "image/gif", "image/webp"]);

/** The content type without its parameters, in lower case. */
const baseType = (type: string) => type.split(";")[0]!.trim().toLowerCase();

/**
 * CLI-16: how a file shows. A photo or a picture document is an image (CLI-89); an HTML document, or one with no clear type
 * and a name ending .html or .htm, is a card that opens in the app (CLI-90); everything else (PDF, SVG, ...) is a download link.
 */
export function fileView(file: FileInfo): "image" | "player" | "video" | "html" | "link" {
  switch (file.kind) {
    case "photo": return "image";
    case "audio":
    case "voice": return "player";
    case "video": return "video";
  }
  const type = baseType(file.type);
  if (pictureTypes.has(type)) return "image";
  if (type === "text/html") return "html";
  if ((type === "" || type === "application/octet-stream") && /\.html?$/i.test(file.name)) return "html";
  return "link";
}

/** CLI-16: a file the server would refuse is refused before it is sent. */
export function fitsSize(bytes: number, maxMb: number): boolean {
  return bytes <= maxMb * 1024 * 1024;
}

/** CLI-15: choosing a command sends it as a message. */
export function commandMessage(command: string): string {
  return `/${command}`;
}

/** CLI-74: the commands whose name starts with what follows "/" (no case), in the bot's order; none once the input has a space or does not start with "/". */
export function suggest<T extends { command: string }>(commands: readonly T[], input: string): T[] {
  if (!input.startsWith("/") || /\s/.test(input)) return [];
  const typed = input.slice(1).toLowerCase();
  return commands.filter(c => c.command.toLowerCase().startsWith(typed));
}

/** CLI-17 and CLI-20: the time under a message, with the "edited" and the "not delivered" marks. */
export function stamp(message: Message, time: string, words: { edited: string; notDelivered: string }): string {
  return [time, ...(message.edited ? [words.edited] : []), ...(message.not_delivered ? [words.notDelivered] : [])].join(" · ");
}

/** CLI-18: decides when the client tells the server how far the user has read. */
export class ReadPosition {
  private sent = 0;

  /** The id to send now, or null: only while the page is visible, and only when it is above the last one sent. */
  next(newestId: number, visible: boolean): number | null {
    if (!visible || newestId <= this.sent) return null;
    this.sent = newestId;
    return newestId;
  }
}

/** Loads one page of a chat. `after`: changes after this version. `before`: messages older than this id. Neither: the newest page. */
export type LoadPage = (botId: number, range: { after?: number; before?: number }) => Promise<Page>;

export class Chat {
  /** In id order, with deleted stubs; see <see cref="visible"/>. */
  messages: Message[] = [];
  /** CLI-14: the message of the pinned bar. */
  pinned: Message | null = null;
  /** CLI-06: the highest version seen; a sync asks for changes after it. */
  version = 0;
  /** False when the server may have messages older than the ones here. */
  reachedStart = false;
  /** Set when the chat is opened. Live messages for a chat that was never opened are dropped: opening loads them. */
  opened = false;

  /** CLI-05: what the screen shows. */
  get visible(): Message[] {
    return this.messages.filter(message => !message.deleted);
  }

  add(incoming: readonly Message[]): void {
    const oldest = this.messages[0]?.id;
    const known = new Map(this.messages.map(message => [message.id, message.version]));
    const fresh: Message[] = [];
    for (const message of incoming) {
      this.version = Math.max(this.version, message.version);
      if ((known.get(message.id) ?? -1) > message.version) continue;

      this.track(message);
      // A change to a message older than the loaded ones would leave a gap in the list.
      if (this.reachedStart || oldest === undefined || message.id >= oldest || known.has(message.id)) fresh.push(message);
    }
    this.messages = merge(this.messages, fresh);
  }

  /** CLI-14: a pinned message becomes the bar; the bar's message unpinned or deleted clears it. */
  private track(message: Message): void {
    if (message.pinned && !message.deleted) this.pinned = message;
    else if (this.pinned?.id === message.id) this.pinned = null;
  }
}

export class Chats {
  private readonly chats = new Map<number, Chat>();
  private readonly load: LoadPage;
  private readonly pageSize: number;

  constructor(load: LoadPage, pageSize: number) {
    this.load = load;
    this.pageSize = pageSize;
  }

  of(botId: number): Chat {
    let chat = this.chats.get(botId);
    if (!chat) this.chats.set(botId, (chat = new Chat()));
    return chat;
  }

  /** The bots whose chat was opened on this device. */
  openedIds(): number[] {
    return [...this.chats].filter(([, chat]) => chat.opened).map(([botId]) => botId);
  }

  /** CLI-06: the first open loads the newest messages, and the pinned one (CLI-14). */
  async open(botId: number): Promise<Chat> {
    const chat = this.of(botId);
    if (chat.opened) return chat;

    // Marked before the request, so a live message that arrives while it runs is kept.
    chat.opened = true;
    try {
      const page = await this.load(botId, {});
      chat.reachedStart = page.messages.length < this.pageSize;
      chat.pinned = page.pinned;
      chat.add(page.messages);
    } catch (error) {
      chat.opened = false;
      throw error;
    }
    return chat;
  }

  /** A message from the live stream, or the answer to a send. */
  receive(botId: number, message: Message): void {
    const chat = this.of(botId);
    if (chat.opened) chat.add([message]);
  }

  /**
   * CLI-06: after the live stream opened again, asks for what was missed in every opened chat.
   * It keeps its own position, so a live message that arrives meanwhile cannot make it skip older changes.
   */
  async sync(): Promise<void> {
    await Promise.all(this.openedIds().map(async botId => {
      const chat = this.of(botId);
      let after = chat.version;
      for (;;) {
        const page = (await this.load(botId, { after })).messages;
        chat.add(page);
        if (page.length < this.pageSize) return;
        after = Math.max(...page.map(message => message.version));
      }
    }));
  }

  /** CLI-14: loads older pages until the message is here. False when it is not in the history (or was deleted). */
  async find(botId: number, messageId: number): Promise<boolean> {
    const chat = this.of(botId);
    const here = () => chat.visible.some(message => message.id === messageId);
    while (!here() && !chat.reachedStart && chat.messages.length > 0) await this.loadOlder(botId);
    return here();
  }

  /** Loads the page before the oldest message that is here. */
  async loadOlder(botId: number): Promise<void> {
    const chat = this.of(botId);
    const oldest = chat.messages[0];
    if (!oldest || chat.reachedStart) return;

    const page = (await this.load(botId, { before: oldest.id })).messages;
    chat.reachedStart = page.length < this.pageSize;
    chat.messages = merge(chat.messages, page);
    for (const message of page) chat.version = Math.max(chat.version, message.version);
  }
}
