// What every screen can use.
import { ApiError, type User } from "./api.ts";
import type { Language, TextKey } from "./i18n.ts";
import type { Chats } from "./messages.ts";

export interface App {
  language: Language;
  user: User;
  chats: Chats;
  maxTextLength: number;
  /** Goes to "/" or "/@name" without a page load. */
  go(path: string): void;
  logout(): void;
}

/** A redraw the open screen asks for when a live message arrives or the stream goes up or down. */
export interface Screen {
  element: HTMLElement;
  onMessage?(botId: number): void;
}

/** The text to show for a failed call. */
export function errorText(error: unknown): TextKey {
  if (!(error instanceof ApiError)) return "failed";
  switch (error.code) {
    case "bot_not_found": return "botNotFound";
    case "text_too_long": return "textTooLong";
    case "rate_limited": return "tooFast";
    case "bad_request": return "badPhone";
    default: return "failed";
  }
}
