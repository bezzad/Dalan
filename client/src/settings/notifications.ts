// CLI-77: the Notifications section: Web Push on this device (CLI-21). The row says what is possible here and has one button.
import { api } from "../api.ts";
import { errorText, type App } from "../app.ts";
import { h } from "../dom.ts";
import { toast } from "../toast.ts";
import { currentPushState, turnOff, turnOn, type PushState } from "../push.ts";
import { settingRow } from "./row.ts";

export function notificationsSection(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  const note = h("p", { class: "note" });
  const button = h("button", { type: "button", hidden: true });
  const element = h("div", { class: "rows" }, settingRow(t("notifications"), t("notificationsNote"), note, button));
  let pushKey: string | null = null;
  let now: PushState = "hidden";

  function show(state: PushState): void {
    now = state;
    button.hidden = state === "hidden" || state === "unsupported" || state === "blocked";
    button.textContent = t(state === "on" ? "notificationsOff" : "notificationsOn");
    note.textContent = t(state === "on" ? "notificationsAreOn" : state === "blocked" ? "pushBlocked" : state === "off" ? "notificationsOffNow" : "pushUnsupported");
    if (state === "hidden") note.textContent = "";
  }
  show("hidden");

  button.addEventListener("click", async () => {
    button.disabled = true;
    try {
      if (now === "on") {
        await turnOff();
        show("off");
      } else {
        show(await turnOn(pushKey!));
      }
    } catch (failure) {
      toast(t(errorText(failure)), "error");
    }
    button.disabled = false;
  });

  api.pushKey().then(async key => {
    pushKey = key;
    show(await currentPushState(key));
  }).catch(() => {});

  return element;
}
