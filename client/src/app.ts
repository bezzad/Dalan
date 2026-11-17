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
  /** CLI-19: how often an open chat asks for the bot's status. */
  statusRefreshSeconds: number;
  /** Goes to "/" or "/@name" without a page load. */
  go(path: string): void;
  /** Shows the account, builder or admin screen over the bot list, or goes back to the list (null). */
  showPanel(panel: Panel | null): void;
  logout(): void;
}

export type Panel = "account" | "builder" | "admin";

/** What the open screen does with live events. */
export interface Screen {
  element: HTMLElement;
  /** A redraw when a message of this chat arrived or changed. */
  onMessage?(botId: number): void;
  /** CLI-13: the bot answered a press with a text. */
  onAnswer?(botId: number, text: string, showAlert: boolean): void;
  /** LIVE-10: the user read a chat on some device. */
  onRead?(botId: number): void;
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
    case "bot_suspended": return "botSuspended";
    case "suspended": return "accountSuspended";
    case "not_builder": return "notBuilder";
    case "bot_limit": return "botLimit";
    case "bad_value": return "badValue";
    case "bad_icon": return "badIcon";
    case "showcase_not_found": return "showcaseFirst";
    default: return "failed";
  }
}
