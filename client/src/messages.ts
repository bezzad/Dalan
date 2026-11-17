// The messages of the chats, as pure logic with no DOM and no network (CLI-05, CLI-06, CLI-07, CLI-13 to CLI-17).

export interface InlineButton {
  text: string;
  callback_data?: string;
  url?: string;
}

export interface FileInfo {
  id: string;
  kind: "photo" | "document" | "audio" | "voice" | "video";
  name: string;
  /** The content type. */
  type: string;
  size: number;
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
  inline_keyboard?: InlineButton[][];
  file?: FileInfo;
  /** Time of the last edit. */
  edited?: string;
  pinned?: boolean;
  deleted?: boolean;
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
  for (let i = list.length - 1; i >= 0; i--) {
    const message = list[i]!;
    if (message.from === "bot" && !message.deleted && message.reply_keyboard) return message.reply_keyboard;
  }
  return [];
}

/** CLI-13: what a tap on an inline button does. */
export function buttonAction(button: InlineButton): { press: string } | { open: string } {
  return button.url !== undefined ? { open: button.url } : { press: button.callback_data ?? "" };
}

/** CLI-16: how a file is shown. */
export function fileView(file: FileInfo): "image" | "audio" | "video" | "link" {
  switch (file.kind) {
    case "photo": return "image";
    case "audio":
    case "voice": return "audio";
    case "video": return "video";
    default: return "link";
  }
}

/** CLI-16: a file the server would refuse is refused before it is sent. */
export function fitsSize(bytes: number, maxMb: number): boolean {
  return bytes <= maxMb * 1024 * 1024;
}

/** CLI-15: choosing a command sends it as a message. */
export function commandMessage(command: string): string {
  return `/${command}`;
}

/** CLI-17: the time under a message, with the "edited" mark when it was edited. */
export function stamp(message: Message, time: string, editedWord: string): string {
  return message.edited ? `${time} · ${editedWord}` : time;
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
