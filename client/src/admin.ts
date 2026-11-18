// The admin screen (CLI-25): settings, builder requests, users, bots, the access log and the audit log.
import { ApiError, api, type AccessRow, type AdminBot, type AdminUser, type ShowcaseRequest } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { h } from "./dom.ts";
import type { TextKey } from "./i18n.ts";
import { categoryName, initial } from "./pages.ts";
import { cleanValue, groupSettings, utc, type Setting } from "./panels.ts";

type Tab = "settings" | "requests" | "users" | "bots" | "showcase" | "access" | "audit";
const Tabs: [Tab, TextKey][] = [
  ["settings", "tabSettings"], ["requests", "tabRequests"], ["users", "tabUsers"],
  ["bots", "tabBots"], ["showcase", "showcase"], ["access", "tabAccess"], ["audit", "tabAudit"],
];

export function adminScreen(app: App): Screen {
  const t = app.language.t.bind(app.language);
  const body = h("div", { class: "scroller" });
  const error = h("p", { class: "error", role: "alert" });
  const failed = (failure: unknown) => {
    error.textContent = failure instanceof ApiError && failure.code === "bad_request" ? t("badSearch") : t(errorText(failure));
  };
  const local = (iso: string | null) => iso ? `${utc(iso)} · ${new Date(iso).toLocaleString(app.language.current === "fa" ? "fa-IR" : "en-GB")}` : "";

  /** A search box over a result list; `run` draws the results for the text. */
  function search(placeholder: string, run: (text: string, results: HTMLElement) => Promise<void>, start = ""): [HTMLFormElement, HTMLElement] {
    const input = h("input", { type: "search", dir: "ltr", placeholder, "aria-label": placeholder, value: start });
    const results = h("div", { class: "column" });
    const form = h("form", {
      class: "row",
      onsubmit: event => {
        event.preventDefault();
        error.textContent = "";
        run(input.value.trim(), results).catch(failed);
      },
    }, input, h("button", { type: "submit", class: "primary" }, t("search")));
    if (start) run(start, results).catch(failed);
    return [form, results];
  }

  const action = (label: TextKey, run: () => Promise<void>) =>
    h("button", { type: "button", onclick: () => { error.textContent = ""; run().catch(failed); } }, t(label));

  // ---- settings ----
  function settingRow(setting: Setting): HTMLElement {
    const input = setting.kind === "switch"
      ? h("select", { "aria-label": setting.key }, h("option", { value: "true" }, "true"), h("option", { value: "false" }, "false"))
      : h("input", { type: setting.kind === "number" ? "number" : "text", dir: "ltr", "aria-label": setting.key, value: setting.value });
    input.value = setting.value;
    const save = h("button", { type: "submit", class: "primary" }, t("save"));
    const check = () => { save.disabled = cleanValue(setting.kind, input.value) === null || input.value === setting.value; };
    input.addEventListener("input", check);
    check();
    const replace = (next: Setting) => row.replaceWith(settingRow(next));
    const row = h("form", {
      class: "setting",
      onsubmit: event => {
        event.preventDefault();
        const value = cleanValue(setting.kind, input.value);
        if (value !== null) api.setSetting(setting.key, value).then(replace, failed);
      },
    },
    h("label", { dir: "ltr" }, setting.key.split(":").slice(1).join(":"),
      ...(setting.restart ? [h("span", { class: "badge" }, t("afterRestart"))] : [])),
    input, save,
    ...(setting.changed
      ? [h("button", { type: "button", onclick: () => api.resetSetting(setting.key).then(replace, failed), title: setting.start }, t("reset"))]
      : []));
    return row;
  }

  async function showSettings(): Promise<void> {
    const groups = groupSettings(await api.adminSettings());
    body.replaceChildren(...groups.map(([name, list]) =>
      h("section", { class: "card" }, h("h2", { dir: "ltr" }, name), ...list.map(settingRow))));
  }

  // ---- users ----
  function userCard(user: AdminUser): HTMLElement {
    const card = h("div", { class: "card" });
    const redraw = (next: AdminUser) => card.replaceWith(userCard(next));
    card.append(
      h("strong", { dir: "auto" }, `${user.name} (#${user.id})`),
      h("span", { dir: "ltr" }, [user.phone, user.email, user.username ? `@${user.username}` : ""].filter(Boolean).join(" · ")),
      h("span", { class: "note" }, `${t("builderState")}: ${t(user.builder === "approved" ? "approved" : user.builder === "requested" ? "requested" : "none")}`
        + (user.suspended ? ` · ${t("suspended")}` : "")),
      h("div", { class: "row wrap" },
        ...(user.builder === "requested" ? [
          action("approve", async () => redraw(await api.setBuilder(user.id, true))),
          action("refuse", async () => redraw(await api.setBuilder(user.id, false))),
        ] : []),
        action(user.suspended ? "unsuspend" : "suspend", async () => {
          if (user.suspended || confirm(t("suspendConfirm"))) redraw(await api.suspendUser(user.id, !user.suspended));
        }),
        action("tabAccess", async () => showAccess("user", String(user.id)))));
    return card;
  }

  function showUsers(): void {
    body.replaceChildren(...search(t("userSearch"), async (text, results) => {
      results.replaceChildren(...(await api.adminUsers(text)).map(userCard));
    }));
  }

  async function showRequests(): Promise<void> {
    const users = await api.adminUsers("");
    body.replaceChildren(...(users.length === 0 ? [h("p", { class: "note" }, t("noRequests"))] : users.map(userCard)));
  }

  // ---- bots ----
  function botCard(bot: AdminBot): HTMLElement {
    const card = h("div", { class: "card" });
    card.append(
      h("strong", { dir: "auto" }, `${bot.name} (#${bot.id})`),
      h("span", { dir: "ltr" }, `@${bot.username} · ${t("owner")} #${bot.owner_user_id}`),
      h("span", { class: "note" }, bot.suspended ? t("suspended") : local(bot.last_seen_at)),
      h("div", { class: "row wrap" },
        action(bot.suspended ? "unsuspend" : "suspend", async () => {
          if (bot.suspended || confirm(t("suspendConfirm"))) card.replaceWith(botCard(await api.suspendBot(bot.id, !bot.suspended)));
        }),
        action("tabAccess", async () => showAccess("bot", String(bot.id)))));
    return card;
  }

  function showBots(): void {
    body.replaceChildren(...search(t("botSearch"), async (text, results) => {
      results.replaceChildren(...(await api.adminBots(text)).map(botCard));
    }));
  }

  // ---- showcase review (ADM-13, CLI-29) ----
  function showcaseCard(entry: ShowcaseRequest): HTMLElement {
    const card = h("div", { class: "card" });
    const review = (approve: boolean) => action(approve ? "approve" : "refuse", async () => {
      await api.reviewShowcase(entry.bot_id, approve);
      card.remove();
    });
    card.append(
      h("div", { class: "line" },
        entry.icon
          ? h("img", { class: "avatar", src: api.ownIconUrl(entry.bot_id), alt: "" })
          : h("span", { class: "avatar" }, initial(entry.name)),
        h("strong", { dir: "auto" }, `${entry.name} (#${entry.bot_id})`)),
      h("span", { dir: "ltr" }, `@${entry.username} · ${t("owner")} #${entry.owner_user_id} · ${categoryName(app.language.current, entry.category)}`
        + (entry.suspended ? ` · ${t("suspended")}` : "")),
      h("p", { dir: "auto" }, entry.description),
      h("div", { class: "row wrap" }, review(true), review(false)));
    return card;
  }

  async function showShowcase(): Promise<void> {
    const entries = await api.showcaseRequests();
    body.replaceChildren(...(entries.length === 0 ? [h("p", { class: "note" }, t("noShowcaseRequests"))] : entries.map(showcaseCard)));
  }

  // ---- access log (ADM-10) ----
  function accessLine(row: AccessRow): HTMLElement {
    const who = row.actor === "phone" ? row.phone ?? "" : row.actor === "email" ? row.email ?? ""
      : `${t(row.actor === "bot" ? "botWord" : "userWord")} #${row.actor_id}`;
    return h("li", { dir: "ltr" },
      h("code", {}, row.port ? `${row.address}:${row.port}` : row.address), ` ${who} · ${local(row.at)}`);
  }

  function showAccess(filter: "user" | "bot" | "phone" | "email" | "address" = "address", value = ""): void {
    select("access", false);
    const kind = h("select", { "aria-label": t("tabAccess") },
      ...(["user", "bot", "phone", "email", "address"] as const).map(name =>
        h("option", { value: name }, t(name === "user" ? "userWord" : name === "bot" ? "botWord" : name))));
    kind.value = filter;
    let before: number | undefined;
    const list = h("ul", { class: "log" });
    const more = h("button", { type: "button", hidden: true }, t("olderRows"));
    const [form, results] = search(t("accessSearch"), async text => {
      before = undefined;
      list.replaceChildren();
      await page(text);
    }, value);
    async function page(text: string): Promise<void> {
      const rows = await api.accessLog(kind.value, text, before);
      list.append(...rows.map(accessLine));
      before = rows.at(-1)?.id;
      more.hidden = rows.length === 0;
      more.onclick = () => void page(text).catch(failed);
    }
    form.prepend(kind);
    results.append(list, more);
    body.replaceChildren(form, results);
  }

  // ---- audit log (ADM-09) ----
  async function showAudit(): Promise<void> {
    const list = h("ul", { class: "log" });
    const more = h("button", { type: "button" }, t("olderRows"));
    let before: number | undefined;
    async function page(): Promise<void> {
      const rows = await api.audit(before);
      list.append(...rows.map(row => h("li", { dir: "ltr" },
        `${local(row.at)} · #${row.user_id} ${row.action} ${row.target}${row.detail ? ` (${row.detail})` : ""}`)));
      before = rows.at(-1)?.id;
      more.hidden = rows.length === 0;
    }
    more.addEventListener("click", () => void page().catch(failed));
    await page();
    body.replaceChildren(list, more);
  }

  const tabs = h("nav", { class: "row wrap tabs" });
  function select(tab: Tab, draw = true): void {
    tabs.replaceChildren(...Tabs.map(([name, label]) =>
      h("button", { type: "button", class: name === tab ? "primary" : "", onclick: () => select(name) }, t(label))));
    if (!draw) return;
    error.textContent = "";
    body.replaceChildren();
    const shows: Record<Tab, () => void | Promise<void>> = {
      settings: showSettings, requests: showRequests, users: showUsers, bots: showBots, showcase: showShowcase, access: () => showAccess(), audit: showAudit,
    };
    Promise.resolve(shows[tab]()).catch(failed);
  }
  select("settings");

  const element = h("main", { class: "admin" },
    h("header", {},
      h("button", { type: "button", class: "link", onclick: () => app.showPanel("account") }, t("back")),
      h("h1", {}, t("adminScreen"))),
    tabs, error, body);

  return { element };
}
