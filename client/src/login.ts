// The login screens: by email code (AUTH-24, AUTH-25), by SMS code (AUTH-11, AUTH-12), by password (AUTH-14),
// and the development login (AUTH-06).
import { ApiError, api, type AuthSettings, type User } from "./api.ts";
import { errorText } from "./app.ts";
import { latinDigits } from "./digits.ts";
import { h } from "./dom.ts";
import type { Language, TextKey } from "./i18n.ts";
import { solve } from "./pow.ts";

type Channel = "sms" | "email";
type Mode = Channel | "password" | "dev";

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
  const emailInput = () => h("input", {
    id: "email", type: "email", dir: "ltr", autocapitalize: "none", autocomplete: "email", placeholder: "sara@example.com", required: true });
  const nameInput = () => h("input", {
    id: "name", type: "text", autocomplete: "name", maxlength: String(settings.name_max_length), required: true });

  /** The two ways to get a code: SMS to a phone, or email (level 1). */
  const channels = {
    sms: {
      title: "loginTitle", note: "loginNote", sent: "codeSent", change: "changePhone", newName: "nameNeeded", label: "phone",
      input: phoneInput, clean: latinDigits,
      ask: async (to: string) => {
        const { challenge, difficulty } = await api.challenge({ phone: to });
        return api.sendCode(to, challenge, await solve(challenge, difficulty));
      },
      verify: api.verifyCode,
    },
    email: {
      title: "emailLoginTitle", note: "emailLoginNote", sent: "emailCodeSent", change: "changeEmail", newName: "emailNameNeeded", label: "email",
      input: emailInput, clean: (text: string) => text.trim(),
      ask: async (to: string) => {
        const { challenge, difficulty } = await api.challenge({ email: to });
        return api.sendEmailCode(to, challenge, await solve(challenge, difficulty));
      },
      verify: api.verifyEmailCode,
    },
  } as const;

  /** Step 1: the phone or email. The browser does the proof of work (CLI-10), then the server sends the code. */
  function askCode(channel: Channel): HTMLElement {
    const way = channels[channel];
    const target = way.input();
    const working = h("p", { class: "note", role: "status" });
    return form(way.title, way.note, "sendCode", [h("label", { for: target.id }, t(way.label)), target, working], async () => {
      const to = way.clean(target.value);
      working.textContent = t("preparing");
      try {
        const sent = await way.ask(to);
        show(enterCode(channel, to, sent.code_digits));
      } finally {
        working.textContent = "";
      }
    });
  }

  /** Step 2: the code. A new phone or email is asked for a name too, and the same code is sent again with it. CLI-33: the email note says "check spam". */
  function enterCode(channel: Channel, to: string, digits: number): HTMLElement {
    const way = channels[channel];
    const code = h("input", {
      id: "code", type: "text", inputmode: "numeric", dir: "ltr", autocomplete: "one-time-code", maxlength: String(digits), required: true });
    const name = nameInput();
    const nameLabel = h("label", { for: "name", hidden: true }, t(way.newName));
    name.hidden = name.disabled = true;

    return form(way.title, way.sent, "login", [
      h("p", { dir: "ltr" }, to),
      h("label", { for: "code" }, t("code")), code,
      nameLabel, name,
      h("button", { type: "button", class: "link", onclick: () => show(askCode(channel)) }, t(way.change)),
    ], async () => {
      try {
        onLogin(await way.verify(to, latinDigits(code.value), name.disabled ? undefined : name.value));
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
    show(mode === "sms" || mode === "email" ? askCode(mode) : mode === "password" ? password() : dev());
    const link = (to: Mode, text: TextKey) => h("button", { type: "button", class: "link", onclick: () => open(to) }, t(text));
    links.replaceChildren(
      ...(mode === "email" || !settings.email_login ? [] : [link("email", "byEmail")]),
      ...(mode === "sms" ? [] : [link("sms", "bySms")]),
      ...(mode === "password" ? [] : [link("password", "byPassword")]),
      ...(mode === "dev" || !settings.dev_login ? [] : [link("dev", "byDevLogin")]),
      h("button", { type: "button", class: "link", onclick: onLanguage }, t("language")),
      h("button", { type: "button", class: "link", onclick: onBack }, t("back")));
  }

  open(settings.email_login ? "email" : "sms");
  return h("main", { class: "login" }, card, links);
}
