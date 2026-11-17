// The account screen: set the username (AUTH-15), the password (AUTH-13), notifications (CLI-21) and building bots (CLI-23).
import { api } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { h } from "./dom.ts";
import { builderView } from "./panels.ts";
import { currentPushState, turnOff, turnOn, type PushState } from "./push.ts";

export function accountScreen(app: App): Screen {
  const t = app.language.t.bind(app.language);

  /** A small form with its own "saved" and error line. */
  function form(fields: (Node | string)[], action: () => Promise<void>): HTMLFormElement {
    const status = h("p", { class: "note", role: "status" });
    const error = h("p", { class: "error", role: "alert" });
    const submit = h("button", { type: "submit", class: "primary" }, t("save"));
    return h("form", {
      class: "card",
      onsubmit: async event => {
        event.preventDefault();
        submit.disabled = true;
        status.textContent = error.textContent = "";
        try {
          await action();
          status.textContent = t("saved");
        } catch (failure) {
          error.textContent = t(errorText(failure));
        }
        submit.disabled = false;
      },
    }, ...fields, status, error, submit);
  }

  const username = h("input", {
    id: "username", type: "text", dir: "ltr", autocapitalize: "none", autocomplete: "username", required: true, value: app.user.username ?? "" });
  const usernameForm = form([
    h("label", { for: "username" }, t("username")), username,
    h("p", { class: "note" }, t("usernameNote")),
  ], async () => {
    app.user = await api.setUsername(username.value.trim());
  });

  const current = h("input", { id: "current", type: "password", dir: "ltr", autocomplete: "current-password" });
  const currentLabel = h("label", { for: "current" }, t("currentPassword"));
  const next = h("input", {
    id: "new", type: "password", dir: "ltr", autocomplete: "new-password", minlength: String(app.auth.password_min_length), required: true });
  const passwordForm = form([
    currentLabel, current,
    h("label", { for: "new" }, t("newPassword")), next,
    h("p", { class: "note" }, t("passwordNote").replace("{n}", String(app.auth.password_min_length))),
  ], async () => {
    await api.setPassword(next.value, current.value || undefined);
    app.user = { ...app.user, has_password: true };
    current.value = next.value = "";
    showCurrent();
  });

  // The current password is asked only when there is one. (After a login by SMS the server does not need it.)
  function showCurrent(): void {
    currentLabel.hidden = current.hidden = !app.user.has_password;
  }
  showCurrent();

  // CLI-21: hidden until the server says it has a push key.
  const pushNote = h("p", { class: "note" });
  const pushError = h("p", { class: "error", role: "alert" });
  const pushButton = h("button", { type: "button", class: "primary", hidden: true });
  const pushCard = h("section", { class: "card", hidden: true },
    h("h2", {}, t("notifications")), pushNote, pushError, pushButton);
  let pushKey: string | null = null;
  let pushNow: PushState = "hidden";

  function showPush(state: PushState): void {
    pushNow = state;
    pushCard.hidden = state === "hidden";
    pushButton.hidden = state === "unsupported" || state === "blocked";
    pushButton.textContent = t(state === "on" ? "notificationsOff" : "notificationsOn");
    pushNote.textContent = t(state === "on" ? "notificationsAreOn" : state === "unsupported" ? "pushUnsupported"
      : state === "blocked" ? "pushBlocked" : "notificationsNote");
  }

  pushButton.addEventListener("click", async () => {
    pushButton.disabled = true;
    pushError.textContent = "";
    try {
      if (pushNow === "on") {
        await turnOff();
        showPush("off");
      } else {
        showPush(await turnOn(pushKey!));
      }
    } catch (failure) {
      pushError.textContent = t(errorText(failure));
    }
    pushButton.disabled = false;
  });

  api.pushKey().then(async key => {
    pushKey = key;
    showPush(await currentPushState(key));
  }).catch(() => {});

  // CLI-23: ask to become a builder, wait for the admin, or open "My bots". Admins also open the admin screen.
  const builderError = h("p", { class: "error", role: "alert" });
  const builderBody = h("div", { class: "column" });
  function showBuilder(): void {
    const view = builderView(app.user);
    builderBody.replaceChildren(
      view === "request"
        ? h("button", {
          type: "button", class: "primary", onclick: async () => {
            builderError.textContent = "";
            try {
              app.user = await api.askBuilder();
              showBuilder();
            } catch (failure) {
              builderError.textContent = t(errorText(failure));
            }
          },
        }, t("askBuilder"))
        : view === "waiting"
          ? h("p", { class: "note" }, t("builderWaiting"))
          : h("button", { type: "button", class: "primary", onclick: () => app.showPanel("builder") }, t("builderBots")),
      ...(app.user.admin ? [h("button", { type: "button", onclick: () => app.showPanel("admin") }, t("adminScreen"))] : []));
  }
  showBuilder();
  const builderCard = h("section", { class: "card" },
    h("h2", {}, t("building")), h("p", { class: "note" }, t("buildingNote")), builderBody, builderError);

  const element = h("main", {},
    h("header", {},
      h("button", { type: "button", class: "link", onclick: () => app.showPanel(null) }, t("back")),
      h("h1", {}, t("account"))),
    h("p", { class: "note", dir: "auto" }, app.user.name),
    h("div", { class: "scroller" }, pushCard, usernameForm, passwordForm, builderCard));

  return { element };
}
