// The development login screen (AUTH-06). Real login by SMS is milestone 4.
import { ApiError, api, type User } from "./api.ts";
import { errorText } from "./app.ts";
import { h } from "./dom.ts";
import type { Language } from "./i18n.ts";

export function loginScreen(language: Language, onLogin: (user: User) => void, onLanguage: () => void): HTMLElement {
  const t = language.t.bind(language);
  const phone = h("input", { id: "phone", type: "tel", inputmode: "tel", dir: "ltr", autocomplete: "tel", placeholder: "09121234567", required: true });
  const name = h("input", { id: "name", type: "text", autocomplete: "name", required: true });
  const error = h("p", { class: "error", role: "alert" });
  const submit = h("button", { type: "submit", class: "primary" }, t("login"));

  const form = h("form", {
    class: "card",
    onsubmit: async event => {
      event.preventDefault();
      submit.disabled = true;
      error.textContent = "";
      try {
        onLogin(await api.devLogin(phone.value, name.value));
      } catch (failure) {
        // 404 = this server has no development login (production before milestone 4).
        error.textContent = t(failure instanceof ApiError && failure.status === 404 ? "loginOff" : errorText(failure));
        submit.disabled = false;
      }
    },
  },
    h("h1", {}, t("appName")),
    h("h2", {}, t("loginTitle")),
    h("p", { class: "note" }, t("loginNote")),
    h("label", { for: "phone" }, t("phone")), phone,
    h("label", { for: "name" }, t("name")), name,
    error,
    submit);

  return h("main", { class: "login" },
    form,
    h("button", { type: "button", class: "link", onclick: onLanguage }, t("language")));
}
