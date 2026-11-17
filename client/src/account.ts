// The account screen: set the username (AUTH-15) and the password (AUTH-13).
import { api } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { h } from "./dom.ts";

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

  const element = h("main", {},
    h("header", {},
      h("button", { type: "button", class: "link", onclick: () => app.showAccount(false) }, t("back")),
      h("h1", {}, t("account"))),
    h("p", { class: "note", dir: "auto" }, app.user.name),
    h("div", { class: "scroller" }, usernameForm, passwordForm));

  return { element };
}
