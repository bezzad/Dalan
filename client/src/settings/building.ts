// CLI-77: the Building group. The Builder section, for every user (CLI-23): a verified phone builds with no request
// (AUTH-39), so it says they can build, the phone note with its button, or the refused note; for a builder or an admin
// the ways to My bots, My apps and the admin screen.
// The Access keys section (the keys and the ready prompt) only for an approved builder or an admin (CLI-30).
import { agentsPart } from "../agents.ts";
import type { App } from "../app.ts";
import { h } from "../dom.ts";
import { builderView } from "../panels.ts";
import { settingRow } from "./row.ts";

export function builderSection(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  const open = (label: string, path: string) => h("button", { type: "button", onclick: () => app.go(path) }, label);
  const view = builderView(app.user);
  const state = view === "bots" ? h("p", { class: "note" }, t("builderCan"))
    : view === "phone" ? h("div", {}, h("p", { class: "note" }, t("phoneRequired")), open(t("verifyPhone"), "/account/profile"))
    : view === "closed" ? h("p", { class: "note" }, t("builderClosed"))
    : h("p", { class: "note" }, t("builderRefused"));
  return h("div", { class: "rows" },
    settingRow(t("builderState"), t("builderStateHelp"), state),
    ...(view === "bots" ? [
      settingRow(t("myBots"), t("myBotsHelp"), open(t("myBots"), "/bots")),
      settingRow(t("myApps"), t("myAppsHelp"), open(t("myApps"), "/apps")),
    ] : []),
    ...(app.user.admin ? [settingRow(t("adminScreen"), t("adminHelp"), open(t("adminScreen"), "/admin"))] : []));
}

export function keysSection(app: App): HTMLElement {
  return h("div", { class: "rows" }, agentsPart(app));
}
