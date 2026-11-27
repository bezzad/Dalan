// CLI-45: the chat page is the bot's own page. One header: the bot's icon, name and status, then search,
// theme, the user menu and the Dalan mark. No Back button: the browser and the Dalan mark go back.
import { h } from "./dom.ts";
import type { Message } from "./messages.ts";
import { initial } from "./pages.ts";
import { icon } from "./public.ts";

/** The loaded messages whose text or file name has the words, without regard to case. Empty words keep every message. */
export function matchingMessages(messages: Message[], query: string): Message[] {
  const words = query.trim().toLocaleLowerCase();
  if (words === "") return messages;
  return messages.filter(message => `${message.text} ${message.file?.name ?? ""}`.toLocaleLowerCase().includes(words));
}

/** CLI-80: the voice button of the chat bar (CLI-46): Dalan's line microphone, not an emoji. */
export function micButton(label: string): HTMLButtonElement {
  return h("button", { type: "button", class: "primary mic", hidden: true, "aria-label": label, title: label }, icon("mic"));
}

/** CLI-91: the bar above the input while the bot is offline. `text` has `{name}`, where `@username` goes. */
export function offlineBar(username: string, text: string): {
  element: HTMLElement;
  /** Shows the bar for an offline bot, hides it for an online or suspended one (CLI-25 says that instead). */
  update(status: { online: boolean; last_seen: string | null }, suspended: boolean): void;
} {
  const [before = "", after = ""] = text.split("{name}");
  const element = h("p", { class: "offline-bar", role: "status", hidden: true },
    before, h("bdi", { dir: "ltr" }, `@${username}`), after);
  return { element, update: (status, suspended) => { element.hidden = status.online || suspended; } };
}

export interface ChatHeader {
  element: HTMLElement;
  /** The line after "@username": "online" or "last seen ..." (CLI-19). */
  status: HTMLElement;
  /** The bot's name, and its showcase icon; with no icon (or a failed one) its first letter. */
  showBot(name: string, iconUrl: string | null): void;
  /** Closes the search and shows every message again (CLI-14: before going to the pinned message). */
  clearSearch(): void;
}

export function chatHeader(options: {
  username: string;
  t: (key: "search" | "close") => string;
  onSearch: (query: string) => void;
  /** The theme button, the user menu and the Dalan mark, in this order. */
  tools: HTMLElement[];
}): ChatHeader {
  const { username, t, onSearch } = options;
  const face = h("span", { class: "face" }, h("span", { class: "avatar", "aria-hidden": "true" }, initial(username)));
  const title = h("h1", { dir: "auto" }, `@${username}`);
  const status = h("span", { role: "status" });
  const who = h("div", { class: "who" }, face,
    h("div", { class: "names" }, title, h("p", { class: "note" }, h("bdi", { dir: "ltr" }, `@${username}`), status)));

  const input = h("input", { type: "search", dir: "auto", placeholder: t("search"), "aria-label": t("search") });
  const close = h("button", { type: "button", class: "round close", "aria-label": t("close"), title: t("close") }, icon("close"));
  const box = h("div", { class: "search", hidden: true }, input, close);
  const open = h("button", { type: "button", class: "round search", "aria-label": t("search"), title: t("search") }, icon("search"));

  const closeSearch = () => {
    box.hidden = true;
    who.hidden = open.hidden = false;
    input.value = "";
    onSearch("");
  };
  open.addEventListener("click", () => {
    who.hidden = open.hidden = true;
    box.hidden = false;
    input.focus();
  });
  input.addEventListener("input", () => onSearch(input.value));
  input.addEventListener("keydown", event => { if ((event as KeyboardEvent).key === "Escape") closeSearch(); });
  close.addEventListener("click", closeSearch);

  return {
    element: h("header", { class: "bot" }, who, box, open, ...options.tools),
    status,
    clearSearch: () => { if (!box.hidden) closeSearch(); },
    showBot(name, iconUrl) {
      title.textContent = name;
      const letter = () => h("span", { class: "avatar", "aria-hidden": "true" }, initial(name));
      face.replaceChildren(iconUrl ? h("img", { class: "avatar", src: iconUrl, alt: "", onerror: () => face.replaceChildren(letter()) }) : letter());
    },
  };
}
