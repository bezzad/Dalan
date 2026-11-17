// The live stream (SSE). It opens again by itself with a growing wait (CLI-08).
import { ApiError, api } from "./api.ts";
import { Backoff } from "./backoff.ts";
import type { Message } from "./messages.ts";

export interface LiveHandlers {
  /** The stream is open (first time or again): time to sync. */
  onOpen(): void;
  onMessage(botId: number, message: Message): void;
  /** The stream is down and the client is waiting to try again. */
  onDown(): void;
  /** The session has ended. */
  onLoggedOut(): void;
}

export class Live {
  private readonly backoff: Backoff;
  private readonly handlers: LiveHandlers;
  private source: EventSource | null = null;
  private timer = 0;
  private stopped = false;

  constructor(backoff: Backoff, handlers: LiveHandlers) {
    this.backoff = backoff;
    this.handlers = handlers;
  }

  start(): void {
    const source = (this.source = new EventSource("/api/events"));
    source.onopen = () => {
      this.backoff.reset();
      this.handlers.onOpen();
    };
    source.addEventListener("message", event => {
      const data = JSON.parse(event.data) as { bot_id: number; message: Message };
      this.handlers.onMessage(data.bot_id, data.message);
    });
    source.onerror = () => {
      // The browser's own retry does not cover every case (for example a 401 or a 503), so we do it ourselves.
      source.close();
      this.handlers.onDown();
      this.timer = window.setTimeout(() => void this.retry(), this.backoff.next() * 1000);
    };
  }

  stop(): void {
    this.stopped = true;
    window.clearTimeout(this.timer);
    this.source?.close();
  }

  private async retry(): Promise<void> {
    try {
      await api.me();
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) return this.handlers.onLoggedOut();
      // Anything else (offline, server restarting): the stream itself will tell.
    }
    if (!this.stopped) this.start();
  }
}
