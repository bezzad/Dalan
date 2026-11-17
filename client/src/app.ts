// What every screen can use.
import { ApiError, type AuthSettings, type User } from "./api.ts";
import type { Language, TextKey } from "./i18n.ts";
import type { Chats } from "./messages.ts";

export interface App {
  language: Language;
  user: User;
  auth: AuthSettings;
  chats: Chats;
  maxTextLength: number;
  /** Goes to "/" or "/@name" without a page load. */
  go(path: string): void;
  /** Shows the account screen (username, password), or goes back from it. */
  showAccount(open: boolean): void;
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
    case "bad_phone": return "badPhone";
    case "wrong_code": return "wrongCode";
    case "sms_wait": return "smsWait";
    case "sms_limit": return "smsLimit";
    case "sms_unavailable": return "smsUnavailable";
    case "wrong_login": return "wrongLogin";
    case "weak_password": return "weakPassword";
    case "wrong_password": return "wrongPassword";
    case "bad_username": return "badUsername";
    case "username_taken": return "usernameTaken";
    default: return "failed";
  }
}
