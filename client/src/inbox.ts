// CLI-96: the bell in the header. The unread count, a panel with the newest notices, "More", and a tap opens a notice's link.
import { api, type Notice } from "./api.ts";
import { h } from "./dom.ts";
import { glyph } from "./icon.ts";
import type { Language } from "./i18n.ts";
import { toast } from "./toast.ts";

/** The badge on the bell: nothing at 0, "99+" over 99. */
export function badgeText(unread: number): string {
  if (unread <= 0) return "";
  return unread > 99 ? "99+" : String(unread);
}

/** A notice opens only a path of this site (never another host, never a script). */
export function noticeLink(link: string | null): string | null {
  return link && link.startsWith("/") && !link.startsWith("//") && !link.includes("\\") ? link : null;
}

/** The newest unread notice shown: opening the panel marks it and every older one read. Null when nothing is unread. */
export function readUpTo(notices: readonly Notice[]): number | null {
  return notices.some(notice => !notice.read) ? Math.max(...notices.map(notice => notice.id)) : null;
}

/** "More" asks for the notices before the oldest one shown. */
export function nextBefore(notices: readonly Notice[]): number | null {
  return notices.length ? Math.min(...notices.map(notice => notice.id)) : null;
}

/** The bell and its panel. The count is read when it is drawn, so it is new on each screen change. */
export function bellMenu(language: Language, go: (path: string) => void): HTMLElement {
  const t = language.t.bind(language);
  const count = h("span", { class: "bell-count", "aria-hidden": "true" });
  const summary = h("summary", { class: "round", title: t("inbox"), "aria-label": t("inbox") }, glyph("bell"), count);
  const list = h("ul", { class: "notices" });
  const more = h("button", { type: "button", class: "small", hidden: true }, t("more"));
  const panel = h("div", { class: "panel" }, h("strong", { class: "head" }, t("inbox")), list, more);
  const menu = h("details", { class: "menu bell" }, summary, panel);
  let shown: Notice[] = [];

  const setCount = (unread: number) => {
    count.textContent = badgeText(unread);
    count.hidden = unread <= 0;
    summary.setAttribute("aria-label", unread > 0 ? `${t("inbox")} (${language.number(unread)})` : t("inbox"));
  };

  const row = (notice: Notice) => {
    const link = noticeLink(notice.link);
    const body = [
      h("span", { class: "title", dir: "auto" }, notice.title),
      ...(notice.text ? [h("span", { class: "note", dir: "auto" }, notice.text)] : []),
      h("span", { class: "note when" }, language.dateTime(notice.at)),
    ];
    const item = link
      ? h("button", { type: "button", class: "item", onclick: () => { menu.open = false; go(link); } }, ...body)
      : h("div", { class: "item" }, ...body);
    if (!notice.read) item.classList.add("unread");
    return h("li", {}, item);
  };

  const draw = (page: Notice[], hasMore: boolean) => {
    shown = [...shown, ...page];
    list.append(...page.map(row));
    if (!shown.length) {
      list.replaceChildren(h("li", { class: "empty" }, glyph("bell"), h("strong", {}, t("inboxEmpty")),
        h("span", { class: "note" }, t("inboxEmptyNote"))));
    }
    more.hidden = !hasMore;
  };

  const open = async () => {
    shown = [];
    list.replaceChildren();
    const page = await api.inbox();
    draw(page.notices, page.more);
    const upTo = readUpTo(page.notices);
    if (upTo !== null) {
      await api.readInbox(upTo);
      setCount(0);
    }
  };

  menu.addEventListener("toggle", () => {
    if (menu.open) open().catch(() => toast(t("inboxFailed"), "error"));
  });
  more.addEventListener("click", () => {
    const before = nextBefore(shown);
    if (before === null) return;
    api.inbox(before).then(page => draw(page.notices, page.more), () => toast(t("inboxFailed"), "error"));
  });

  setCount(0);
  // The count of this screen; an error leaves the bell plain (the stream or the next screen tries again).
  api.inbox().then(page => setCount(page.unread), () => {});
  return menu;
}
