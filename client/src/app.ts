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
  maxFileSizeMb: number;
  /** Goes to "/" or "/@name" without a page load. */
  go(path: string): void;
  /** Shows the account screen (username, password), or goes back from it. */
  showAccount(open: boolean): void;
  logout(): void;
}

/** What the open screen does with live events. */
export interface Screen {
  element: HTMLElement;
  /** A redraw when a message of this chat arrived or changed. */
  onMessage?(botId: number): void;
  /** CLI-13: the bot answered a press with a text. */
  onAnswer?(botId: number, text: string, showAlert: boolean): void;
}

/** The text to show for a failed call. */
export function errorText(error: unknown): TextKey {
  if (!(error instanceof ApiError)) return "failed";
  switch (error.code) {
    case "bot_not_found": return "botNotFound";
    case "text_too_long": return "textTooLong";
    case "file_too_big": return "fileTooBig";
    case "upload_limit": return "uploadLimit";
    case "button_not_found": return "buttonGone";
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
