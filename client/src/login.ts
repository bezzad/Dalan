// The login screens: by SMS code (AUTH-11, AUTH-12), by password (AUTH-14), and the development login (AUTH-06).
import { ApiError, api, type AuthSettings, type User } from "./api.ts";
import { errorText } from "./app.ts";
import { latinDigits } from "./digits.ts";
import { h } from "./dom.ts";
import type { Language, TextKey } from "./i18n.ts";
import { solve } from "./pow.ts";

type Mode = "sms" | "password" | "dev";

export function loginScreen(
  language: Language, settings: AuthSettings, onLogin: (user: User) => void, onLanguage: () => void, onBack: () => void): HTMLElement {
  const t = language.t.bind(language);
  const card = h("div", {});
  const links = h("p", { class: "links" });

  /** A card with a title, fields and one button. `action` runs on submit; a failed call shows its text. */
  function form(title: TextKey, note: TextKey, button: TextKey, fields: (Node | string)[], action: () => Promise<void>): HTMLElement {
    const error = h("p", { class: "error", role: "alert" });
    const submit = h("button", { type: "submit", class: "primary" }, t(button));
    return h("form", {
      class: "card",
      onsubmit: async event => {
        event.preventDefault();
        submit.disabled = true;
        error.textContent = "";
        try {
          await action();
        } catch (failure) {
          error.textContent = t(errorText(failure));
        }
        submit.disabled = false;
      },
    }, h("h1", {}, t("appName")), h("h2", {}, t(title)), h("p", { class: "note" }, t(note)), ...fields, error, submit);
  }

  const phoneInput = () => h("input", {
    id: "phone", type: "tel", inputmode: "tel", dir: "ltr", autocomplete: "tel", placeholder: "09121234567", required: true });
  const nameInput = () => h("input", {
    id: "name", type: "text", autocomplete: "name", maxlength: String(settings.name_max_length), required: true });

  /** Step 1: the phone number. The browser does the proof of work (CLI-10), then the server sends the code. */
  function smsPhone(): HTMLElement {
    const phone = phoneInput();
    const working = h("p", { class: "note", role: "status" });
    return form("loginTitle", "loginNote", "sendCode", [h("label", { for: "phone" }, t("phone")), phone, working], async () => {
      const number = latinDigits(phone.value);
      working.textContent = t("preparing");
      try {
        const { challenge, difficulty } = await api.challenge(number);
        const sent = await api.sendCode(number, challenge, await solve(challenge, difficulty));
        show(smsCode(number, sent.code_digits));
      } finally {
        working.textContent = "";
      }
    });
  }

  /** Step 2: the code. A new number is asked for a name too, and the same code is sent again with it. */
  function smsCode(phone: string, digits: number): HTMLElement {
    const code = h("input", {
      id: "code", type: "text", inputmode: "numeric", dir: "ltr", autocomplete: "one-time-code", maxlength: String(digits), required: true });
    const name = nameInput();
    const nameLabel = h("label", { for: "name", hidden: true }, t("nameNeeded"));
    name.hidden = name.disabled = true;

    return form("loginTitle", "codeSent", "login", [
      h("p", { dir: "ltr" }, phone),
      h("label", { for: "code" }, t("code")), code,
      nameLabel, name,
      h("button", { type: "button", class: "link", onclick: () => show(smsPhone()) }, t("changePhone")),
    ], async () => {
      try {
        onLogin(await api.verifyCode(phone, latinDigits(code.value), name.disabled ? undefined : name.value));
      } catch (failure) {
        if (!(failure instanceof ApiError && failure.code === "name_required")) throw failure;
        nameLabel.hidden = name.hidden = name.disabled = false;
        name.focus();
      }
    });
  }

  function password(): HTMLElement {
    const login = h("input", { id: "login", type: "text", dir: "ltr", autocapitalize: "none", autocomplete: "username", required: true });
    const secret = h("input", { id: "password", type: "password", dir: "ltr", autocomplete: "current-password", required: true });
    return form("passwordLoginTitle", "loginName", "login", [
      h("label", { for: "login" }, t("loginName")), login,
      h("label", { for: "password" }, t("password")), secret,
    ], async () => onLogin(await api.passwordLogin(latinDigits(login.value), secret.value)));
  }

  function dev(): HTMLElement {
    const phone = phoneInput();
    const name = nameInput();
    return form("devLoginTitle", "devLoginNote", "login", [
      h("label", { for: "phone" }, t("phone")), phone,
      h("label", { for: "name" }, t("name")), name,
    ], async () => onLogin(await api.devLogin(latinDigits(phone.value), name.value)));
  }

  function show(form: HTMLElement): void {
    card.replaceChildren(form);
    form.querySelector("input")?.focus();
  }

  function open(mode: Mode): void {
    show(mode === "sms" ? smsPhone() : mode === "password" ? password() : dev());
    const link = (to: Mode, text: TextKey) => h("button", { type: "button", class: "link", onclick: () => open(to) }, t(text));
    links.replaceChildren(
      ...(mode === "sms" ? [] : [link("sms", "bySms")]),
      ...(mode === "password" ? [] : [link("password", "byPassword")]),
      ...(mode === "dev" || !settings.dev_login ? [] : [link("dev", "byDevLogin")]),
      h("button", { type: "button", class: "link", onclick: onLanguage }, t("language")),
      h("button", { type: "button", class: "link", onclick: onBack }, t("back")));
  }

  open("sms");
  return h("main", { class: "login" }, card, links);
}
