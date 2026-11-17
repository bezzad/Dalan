// The messages of the chats, as pure logic with no DOM and no network (CLI-05, CLI-06, CLI-07).

export interface Message {
  /** The sequence number in the chat. */
  id: number;
  from: "user" | "bot";
  text: string;
  date: string;
  /** Rows of button texts. Missing = this message does not change the buttons. Empty = remove them. */
  reply_keyboard?: string[][];
}

/** CLI-05: one list in rising id order, each id once. */
export function merge(list: readonly Message[], incoming: readonly Message[]): Message[] {
  const byId = new Map<number, Message>();
  for (const message of list) byId.set(message.id, message);
  for (const message of incoming) byId.set(message.id, message);
  return [...byId.values()].sort((a, b) => a.id - b.id);
}

/** CLI-07: the buttons under the input come from the newest bot message that carries a keyboard. */
export function currentKeyboard(list: readonly Message[]): string[][] {
  for (let i = list.length - 1; i >= 0; i--) {
    const keyboard = list[i]!.reply_keyboard;
    if (list[i]!.from === "bot" && keyboard) return keyboard;
  }
  return [];
}

/** Loads one page of a chat. `after`: messages newer than this id. `before`: older than this id. Neither: the newest page. */
export type LoadPage = (botId: number, range: { after?: number; before?: number }) => Promise<Message[]>;

export class Chat {
  messages: Message[] = [];
  /** False when the server may have messages older than the ones here. */
  reachedStart = false;
  /** Set when the chat is opened. Live messages for a chat that was never opened are dropped: opening loads them. */
  opened = false;

  get newestId(): number {
    return this.messages.at(-1)?.id ?? 0;
  }

  add(incoming: readonly Message[]): void {
    this.messages = merge(this.messages, incoming);
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

  /** CLI-06: the first open loads the newest messages. */
  async open(botId: number): Promise<Chat> {
    const chat = this.of(botId);
    if (chat.opened) return chat;

    // Marked before the request, so a live message that arrives while it runs is kept.
    chat.opened = true;
    try {
      const page = await this.load(botId, {});
      chat.add(page);
      chat.reachedStart = page.length < this.pageSize;
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
   * It keeps its own position, so a live message that arrives meanwhile cannot make it skip older ones.
   */
  async sync(): Promise<void> {
    await Promise.all(this.openedIds().map(async botId => {
      const chat = this.of(botId);
      let after = chat.newestId;
      for (;;) {
        const page = await this.load(botId, { after });
        chat.add(page);
        if (page.length < this.pageSize) return;
        after = page.at(-1)!.id;
      }
    }));
  }

  /** Loads the page before the oldest message that is here. */
  async loadOlder(botId: number): Promise<void> {
    const chat = this.of(botId);
    const oldest = chat.messages[0];
    if (!oldest || chat.reachedStart) return;

    const page = await this.load(botId, { before: oldest.id });
    chat.add(page);
    chat.reachedStart = page.length < this.pageSize;
  }
}
