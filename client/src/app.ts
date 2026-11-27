// What every screen can use.
import { ApiError, type AuthSettings, type User } from "./api.ts";
import type { Language, TextKey } from "./i18n.ts";
import type { Chats } from "./messages.ts";
import type { Theme } from "./theme.ts";

export interface App {
  language: Language;
  theme: Theme;
  user: User;
  auth: AuthSettings;
  chats: Chats;
  maxTextLength: number;
  maxFileSizeMb: number;
  /** CLI-90: HTML files open in the app; false shows only Download. */
  fileView: boolean;
  /** CLI-19: how often an open chat asks for the bot's status. */
  statusRefreshSeconds: number;
  /** REL-51: the size limit of a bot icon. */
  maxBotIconKb: number;
  /** CLI-53: how long a pressed button waits for the bot. */
  pressWaitSeconds: number;
  /** CLI-51: goes to the screen of an address ("/chats", "/bots/new", "/@name") without a page load. */
  go(path: string): void;
  logout(): void;
}

/** LIVE-09: the data of an "answer" event. */
export interface PressAnswer {
  callback_id: string;
  text?: string;
  show_alert: boolean;
}

/** What the open screen does with live events. */
export interface Screen {
  element: HTMLElement;
  /** A redraw when a message of this chat arrived or changed. */
  onMessage?(botId: number): void;
  /** CLI-13 and CLI-53: the bot answered a press, maybe with a text. */
  onAnswer?(botId: number, answer: PressAnswer): void;
  /** LIVE-10: the user read a chat on some device. */
  onRead?(botId: number): void;
}

/** The text to show for a failed call. */
/** CLI-36: the error the GitHub login sent back in the address ("?login_error=..."), or null. */
export function loginErrorText(search: string): TextKey | null {
  const code = new URLSearchParams(search).get("login_error");
  return code ? errorText(new ApiError(0, code)) : null;
}

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
    case "bad_email": return "badEmail";
    case "bad_text": return "badContactText";
    case "too_many_messages": return "tooManyMessages";
    case "email_wait": return "emailWait";
    case "email_limit": return "emailLimit";
    case "email_unavailable": return "emailUnavailable";
    case "phone_required": return "phoneRequired";
    case "phone_taken": return "phoneTaken";
    case "has_phone": return "hasPhone";
    case "has_email": return "hasEmail";
    case "has_github": return "hasGithub";
    case "merge_needed": return "mergeNeeded";
    case "no_merge": return "noMerge";
    case "both_have_products": return "bothHaveProducts";
    case "wrong_login": return "wrongLogin";
    case "weak_password": return "weakPassword";
    case "wrong_password": return "wrongPassword";
    case "bad_username": return "badUsername";
    case "username_taken": return "usernameTaken";
    case "bot_suspended": return "botSuspended";
    case "suspended": return "accountSuspended";
    case "github_state": return "githubState";
    case "github_denied": return "githubDenied";
    case "github_no_email": return "githubNoEmail";
    case "github_unavailable": return "githubUnavailable";
    case "not_builder": return "notBuilder";
    case "bot_limit": return "botLimit";
    case "name_taken": return "nameTaken";
    case "bad_name": return "badSiteName";
    case "site_limit": return "siteLimit";
    case "sites_off": return "sitesOff";
    case "bad_value": return "badValue";
    case "bad_icon": return "badIcon";
    case "showcase_not_found": return "showcaseFirst";
    default: return "failed";
  }
}
