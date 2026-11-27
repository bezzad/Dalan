// CLI-77: the Building group. The Builder section, for every user (CLI-23): the state with the request button, the waiting
// note or the phone note, and for an approved builder or an admin the ways to My bots, My apps and the admin screen.
// The Access keys section (the keys and the ready prompt) only for an approved builder or an admin (CLI-30).
import { agentsPart } from "../agents.ts";
import { api } from "../api.ts";
import { errorText, type App } from "../app.ts";
import { h } from "../dom.ts";
import { builderView } from "../panels.ts";
import { toast } from "../toast.ts";
import { settingRow } from "./row.ts";

export function builderSection(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  const element = h("div", { class: "rows" });
  const open = (label: string, path: string) => h("button", { type: "button", onclick: () => app.go(path) }, label);
  function draw(): void {
    const view = builderView(app.user);
    const ask = h("button", {
      type: "button", onclick: async () => {
        ask.disabled = true;
        try {
          app.user = await api.askBuilder();
          draw();
        } catch (failure) {
          ask.disabled = false;
          toast(t(errorText(failure)), "error");
        }
      },
    }, t("askBuilder"));
    const state = view === "bots" ? h("span", { class: "badge ok" }, t("builderApproved"))
      : view === "waiting" ? h("p", { class: "note" }, t("builderWaiting"))
      : view === "phone" ? h("p", { class: "note" }, t("phoneRequired"))
      : view === "refused" ? h("div", {}, h("p", { class: "note" }, t("builderRefused")), ask)
      : ask;
    element.replaceChildren(
      settingRow(t("builderState"), t("builderStateHelp"), state),
      ...(view === "bots" ? [
        settingRow(t("myBots"), t("myBotsHelp"), open(t("myBots"), "/bots")),
        settingRow(t("myApps"), t("myAppsHelp"), open(t("myApps"), "/apps")),
      ] : []),
      ...(app.user.admin ? [settingRow(t("adminScreen"), t("adminHelp"), open(t("adminScreen"), "/admin"))] : []));
  }
  draw();
  return element;
}

export function keysSection(app: App): HTMLElement {
  return h("div", { class: "rows" }, agentsPart(app));
}
