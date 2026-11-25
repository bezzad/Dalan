// CLI-77: the Building group, for an approved builder or an admin (CLI-23, CLI-30): the Builder section (state and the ways
// to My bots, My apps and, for an admin, the admin screen) and the Access keys section (the keys and the ready prompt).
import { agentsPart } from "../agents.ts";
import type { App } from "../app.ts";
import { h } from "../dom.ts";
import { settingRow } from "./row.ts";

export function builderSection(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  const open = (label: string, path: string) => h("button", { type: "button", onclick: () => app.go(path) }, label);
  return h("div", { class: "rows" },
    settingRow(t("builderState"), t("builderStateHelp"), h("span", { class: "badge ok" }, t("builderApproved"))),
    settingRow(t("myBots"), t("myBotsHelp"), open(t("myBots"), "/bots")),
    settingRow(t("myApps"), t("myAppsHelp"), open(t("myApps"), "/apps")),
    ...(app.user.admin ? [settingRow(t("adminScreen"), t("adminHelp"), open(t("adminScreen"), "/admin"))] : []));
}

export function keysSection(app: App): HTMLElement {
  return h("div", { class: "rows" }, agentsPart(app));
}
