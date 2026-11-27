// The admin pages of the dashboard (CLI-81): overview (CLI-82), builder requests, users, bots, showcase review, contact
// messages (ADM-18), settings (CLI-83), the access log and the audit log. The server still checks every call (SEC-14).
import { ApiError, api, type AccessRow, type AdminBot, type AdminOverview, type AdminUser, type ContactMessage, type ShowcaseRequest } from "./api.ts";
import { errorText, type App } from "./app.ts";
import { h } from "./dom.ts";
import { toast } from "./toast.ts";
import type { TextKey } from "./i18n.ts";
import { categoryName, initial } from "./pages.ts";
import { cleanValue, settingGroups, settingMatches, settingName, utc, type Setting } from "./panels.ts";
import { adminDetailPath, sectionPath, type SectionId } from "./settings/model.ts";
import { botCells, userCells } from "./adminTables.ts";

/** The admin sections of the dashboard (settings/model.ts). */
export type AdminSection = Extract<SectionId, "overview" | "requests" | "users" | "adminBots" | "showcase" | "messages" | "adminSettings" | "access" | "audit">;

/** `sub` is the id of one user or bot (CLI-93). */
export function adminPage(app: App, section: AdminSection, sub?: string): HTMLElement {
  const t = app.language.t.bind(app.language);
  const lang = app.language.current;
  const body = h("div", { class: "column admin" });
  const error = h("p", { class: "error", role: "alert" });
  const failed = (failure: unknown) => {
    toast(failure instanceof ApiError && failure.code === "bad_request" ? t("badSearch") : t(errorText(failure)), "error");
  };
  const local = (iso: string | null) => iso ? `${utc(iso)} · ${app.language.dateTime(iso)}` : "";

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
    h("button", { type: "button", onclick: () => { run().then(() => toast(t("saved")), failed); } }, t(label));

  // ---- overview (CLI-82) ----
  async function showOverview(): Promise<void> {
    const counts: AdminOverview = await api.adminOverview();
    const card = (value: number, label: TextKey, note: string, section: SectionId) => h("a", {
      class: "card stat", href: sectionPath(section),
      onclick: (event: Event) => { event.preventDefault(); app.go(sectionPath(section)); },
    }, h("strong", { class: "number" }, String(value)), h("span", {}, t(label)), ...(note ? [h("span", { class: "note" }, note)] : []));
    body.replaceChildren(h("div", { class: "stats" },
      card(counts.users, "statUsers", t("statNewWeek").replace("{n}", String(counts.users_new_7d)), "users"),
      card(counts.bots, "statBots", t("statOnline").replace("{n}", String(counts.bots_online)), "adminBots"),
      card(counts.builder_requests + counts.showcase_requests, "statRequests",
        `${t("tabRequests")} ${counts.builder_requests} · ${t("showcase")} ${counts.showcase_requests}`, "requests"),
      card(counts.unread_messages, "statMessages", "", "messages"),
      card(counts.sites_online, "statSites", "", "overview")));
  }

  // ---- settings (CLI-83, ADM-19) ----
  function settingRow(setting: Setting): HTMLElement {
    const number = setting.kind === "number";
    const input = setting.kind === "switch"
      ? h("select", { "aria-label": settingName(setting, lang) }, h("option", { value: "true" }, t("on")), h("option", { value: "false" }, t("off")))
      : h("input", {
        type: number ? "number" : "text", dir: "ltr", "aria-label": settingName(setting, lang), value: setting.value,
        ...(number && setting.min !== undefined && setting.min !== null ? { min: String(setting.min) } : {}),
        ...(number && setting.max !== undefined && setting.max !== null ? { max: String(setting.max) } : {}),
      });
    input.value = setting.value;
    // A row's Save is primary only while its value changed (CLI-77).
    const save = h("button", { type: "submit" }, t("save"));
    const check = () => {
      const changed = input.value !== setting.value;
      save.disabled = !changed || cleanValue(setting.kind, input.value) === null;
      save.classList.toggle("primary", changed);
    };
    input.addEventListener("input", check);
    input.addEventListener("change", check);
    check();
    const replace = (next: Setting) => row.replaceWith(settingRow(next));
    const range = number && (setting.min ?? null) !== null && (setting.max ?? null) !== null ? `${setting.min} – ${setting.max}` : "";
    const row = h("form", {
      class: "setting-row admin-setting",
      onsubmit: event => {
        event.preventDefault();
        const value = cleanValue(setting.kind, input.value);
        if (value === null) return;
        // A risky setting asks first, with the old and the new value.
        if (setting.risky && !confirm(`${t("riskyConfirm")}\n${setting.value || "—"} → ${value}`)) return;
        api.setSetting(setting.key, value).then(replace, failed);
      },
    },
    h("div", { class: "what" },
      h("h3", { dir: "auto" }, settingName(setting, lang)),
      ...(setting.help ? [h("p", { class: "note", dir: "auto" }, setting.help[lang] ?? setting.help.en ?? "")] : []),
      h("code", { class: "key", dir: "ltr" }, setting.key),
      h("div", { class: "row wrap badges" },
        ...(setting.changed ? [h("span", { class: "badge off", title: setting.start }, `${t("changed")} · ${t("startValue")} ${setting.start || "—"}`)] : []),
        ...(setting.restart ? [h("span", { class: "badge off" }, t("afterRestart"))] : []))),
    h("div", { class: "control" },
      h("div", { class: "row" }, input, ...(setting.unit ? [h("span", { class: "note" }, setting.unit)] : [])),
      ...(range ? [h("span", { class: "note", dir: "ltr" }, range)] : []),
      h("div", { class: "row" }, save,
        ...(setting.changed ? [h("button", { type: "button", onclick: () => api.resetSetting(setting.key).then(replace, failed) }, t("reset"))] : []))));
    return row;
  }

  async function showSettings(): Promise<void> {
    const all = await api.adminSettings();
    const find = h("input", { type: "search", placeholder: t("settingSearch"), "aria-label": t("settingSearch") });
    const groups = h("div", { class: "column" });
    const draw = () => groups.replaceChildren(...settingGroups(all.filter(setting => settingMatches(setting, find.value))).map(([name, list]) =>
      h("section", { class: "card" }, h("h2", { dir: "ltr" }, name), h("div", { class: "rows" }, ...list.map(settingRow)))));
    find.addEventListener("input", draw);
    draw();
    body.replaceChildren(find, groups);
  }

  // ---- users and bots: tables of everyone, a page at a time, and a page for each (CLI-93, ADM-11, ADM-12, ADM-20) ----
  const day = (iso: string) => app.language.day(iso);
  const go = (path: string) => (event: Event) => { event.preventDefault(); app.go(path); };

  /** A table whose rows open `path(row)`. */
  function table<T>(heads: TextKey[], rows: T[], cells: (row: T) => string[], path: (row: T) => string, suspended: (row: T) => boolean): HTMLElement {
    const body = h("tbody", {}, ...rows.map(row => {
      const [first, ...rest] = cells(row);
      return h("tr", { class: suspended(row) ? "suspended" : "", onclick: go(path(row)) },
        h("td", {}, h("a", { href: path(row), dir: "auto", onclick: go(path(row)) }, first ?? ""),
          ...(suspended(row) ? [" ", h("span", { class: "badge off" }, t("suspended"))] : [])),
        ...rest.map(cell => h("td", { dir: "auto" }, cell)));
    }));
    return h("div", { class: "table-wrap" }, h("table", { class: "admin-table" },
      h("thead", {}, h("tr", {}, ...heads.map(head => h("th", {}, t(head))))), body));
  }

  /** A list page: the search, the table and More. With no search it is everyone, newest first. */
  function listPage<T extends { id: number }>(placeholder: string, load: (query: string, before?: number) => Promise<T[]>,
    draw: (rows: T[]) => HTMLElement): void {
    const input = h("input", { type: "search", dir: "auto", placeholder, "aria-label": placeholder });
    const rows: T[] = [];
    const place = h("div", { class: "column" });
    const more = h("button", { type: "button", hidden: true }, t("moreRows"));
    let query = "";
    async function page(fresh: boolean): Promise<void> {
      const next = await load(query, fresh ? undefined : rows.at(-1)?.id);
      if (fresh) rows.length = 0;
      rows.push(...next);
      place.replaceChildren(rows.length === 0 ? h("p", { class: "note" }, t("noResults")) : draw(rows));
      more.hidden = next.length === 0;
    }
    more.addEventListener("click", () => void page(false).catch(failed));
    const form = h("form", { class: "row", onsubmit: event => {
      event.preventDefault();
      query = input.value.trim();
      page(true).catch(failed);
    } }, input, h("button", { type: "submit" }, t("search")));
    body.replaceChildren(form, place, more);
    page(true).catch(failed);
  }

  const userTable = (users: AdminUser[]) => table(["colName", "colContact", "builderState", "tabBots", "colJoined", "colLastActive"], users,
    user => userCells(user, t, day), user => adminDetailPath("users", user.id), user => user.suspended);
  const botTable = (bots: AdminBot[]) => table(["colName", "owner", "colUsers", "colLastSeen"], bots,
    bot => botCells(bot, day), bot => adminDetailPath("adminBots", bot.id), bot => bot.suspended);

  /** A page's details as a list of names and values. */
  const facts = (...pairs: [TextKey, Node | string][]) => h("dl", { class: "facts" },
    ...pairs.flatMap(([name, value]) => [h("dt", {}, t(name)), h("dd", { dir: "auto" }, value)]));
  const back = (section: "users" | "adminBots") => h("a", { class: "back", href: sectionPath(section), onclick: go(sectionPath(section)) },
    "‹ ", t(section === "users" ? "tabUsers" : "tabBots"));

  function userActions(user: AdminUser, redraw: (next: AdminUser) => void): HTMLElement {
    return h("div", { class: "row wrap" },
      ...(user.builder === "requested" ? [
        action("approve", async () => redraw(await api.setBuilder(user.id, true))),
        action("refuse", async () => redraw(await api.setBuilder(user.id, false))),
      ] : []),
      // CLI-84: the admin may change a "no" into a "yes".
      ...(user.builder === "refused" ? [action("approve", async () => redraw(await api.setBuilder(user.id, true)))] : []),
      action(user.suspended ? "unsuspend" : "suspend", async () => {
        if (user.suspended || confirm(t("suspendConfirm"))) redraw(await api.suspendUser(user.id, !user.suspended));
      }),
      action("tabAccess", async () => showAccess("user", String(user.id))));
  }

  /** The builder requests stay cards, with their two buttons (AUTH-19). */
  function userCard(user: AdminUser): HTMLElement {
    const card = h("div", { class: "card" });
    card.append(
      h("strong", {}, h("a", { href: adminDetailPath("users", user.id), dir: "auto", onclick: go(adminDetailPath("users", user.id)) }, `${user.name} (#${user.id})`)),
      h("span", { dir: "ltr" }, [user.phone, user.email, user.username ? `@${user.username}` : ""].filter(Boolean).join(" · ")),
      userActions(user, next => card.replaceWith(userCard(next))));
    return card;
  }

  function showUsers(): void {
    listPage(t("userSearch"), (query, before) => api.adminUsers(query, before), userTable);
  }

  async function showUser(userId: number): Promise<void> {
    const { user, bots } = await api.adminUser(userId);
    const draw = (shown: AdminUser) => body.replaceChildren(back("users"),
      h("section", { class: "card column" },
        h("h2", { dir: "auto" }, shown.name, ...(shown.suspended ? [" ", h("span", { class: "badge off" }, t("suspended"))] : [])),
        facts(["colId", `#${shown.id}`], ["username", shown.username ? `@${shown.username}` : "—"], ["phone", shown.phone ?? "—"],
          ["email", shown.email ?? "—"], ["builderState", userCells(shown, t, day)[2]!], ["colJoined", local(shown.created_at)],
          ["colLastActive", shown.last_active_at ? local(shown.last_active_at) : "—"]),
        userActions(shown, draw)),
      h("section", { class: "column" }, h("h3", {}, `${t("tabBots")} (${bots.length})`),
        bots.length === 0 ? h("p", { class: "note" }, t("noResults")) : botTable(bots)));
    draw(user);
  }

  async function showRequests(): Promise<void> {
    const users = await api.builderRequests();
    body.replaceChildren(...(users.length === 0 ? [h("p", { class: "note" }, t("noRequests"))] : users.map(userCard)));
  }

  function showBots(): void {
    listPage(t("botSearch"), (query, before) => api.adminBots(query, before), botTable);
  }

  async function showBot(botId: number): Promise<void> {
    const { bot, owner } = await api.adminBot(botId);
    const draw = (shown: AdminBot) => body.replaceChildren(back("adminBots"),
      h("section", { class: "card column" },
        h("h2", { dir: "auto" }, shown.name, ...(shown.suspended ? [" ", h("span", { class: "badge off" }, t("suspended"))] : [])),
        facts(["colId", `#${shown.id}`], ["username", h("a", { href: `/@${shown.username}`, dir: "ltr" }, `@${shown.username}`)],
          ["owner", h("a", { href: adminDetailPath("users", owner.id), dir: "auto", onclick: go(adminDetailPath("users", owner.id)) },
            `${owner.name} (#${owner.id})`)],
          ["colUsers", String(shown.users ?? 0)], ["colJoined", local(shown.created_at)],
          ["colLastSeen", shown.last_seen_at ? local(shown.last_seen_at) : "—"]),
        h("div", { class: "row wrap" },
          action(shown.suspended ? "unsuspend" : "suspend", async () => {
            if (shown.suspended || confirm(t("suspendConfirm"))) draw(await api.suspendBot(shown.id, !shown.suspended));
          }),
          action("tabAccess", async () => showAccess("bot", String(shown.id))))));
    draw(bot);
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

  // ---- contact messages (ADM-18) ----
  function messageCard(message: ContactMessage): HTMLElement {
    const card = h("div", { class: message.read ? "card" : "card unread" });
    card.append(
      h("strong", { dir: "ltr" }, h("a", { href: `mailto:${message.email}` }, message.email)),
      h("span", { class: "note", dir: "ltr" }, [
        message.user_id ? `${t("userWord")} #${message.user_id}` : "",
        message.port ? `${message.address}:${message.port}` : message.address,
        local(message.at),
      ].filter(Boolean).join(" · ")),
      h("p", { dir: "auto", class: "text" }, message.text),
      h("div", { class: "row wrap" },
        action(message.read ? "markUnread" : "markRead", async () =>
          card.replaceWith(messageCard(await api.markMessage(message.id, !message.read))))));
    return card;
  }

  async function showMessages(): Promise<void> {
    const list = h("div", { class: "column" });
    const more = h("button", { type: "button" }, t("olderRows"));
    let before: number | undefined;
    async function page(): Promise<void> {
      const rows = await api.contactMessages(before);
      list.append(...rows.map(messageCard));
      if (list.childElementCount === 0) list.append(h("p", { class: "note" }, t("noMessages")));
      before = rows.at(-1)?.id;
      more.hidden = rows.length === 0;
    }
    more.addEventListener("click", () => void page().catch(failed));
    await page();
    body.replaceChildren(list, more);
  }

  // ---- access log (ADM-10) ----
  function accessLine(row: AccessRow): HTMLElement {
    const who = row.actor === "phone" ? row.phone ?? "" : row.actor === "email" ? row.email ?? ""
      : `${t(row.actor === "bot" ? "botWord" : "userWord")} #${row.actor_id}`;
    return h("li", { dir: "ltr" },
      h("code", {}, row.port ? `${row.address}:${row.port}` : row.address), ` ${who} · ${local(row.at)}`);
  }

  function showAccess(filter: "user" | "bot" | "phone" | "email" | "address" = "address", value = ""): void {
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

  const shows: Record<AdminSection, () => void | Promise<void>> = {
    overview: showOverview, requests: showRequests, users: showUsers, adminBots: showBots, showcase: showShowcase,
    messages: showMessages, adminSettings: showSettings, access: () => showAccess(), audit: showAudit,
  };
  const id = sub ? Number(sub) : null;
  const show = id && section === "users" ? () => showUser(id) : id && section === "adminBots" ? () => showBot(id) : shows[section];
  Promise.resolve(show()).catch(failed);
  return h("div", { class: "column" }, error, body);
}
