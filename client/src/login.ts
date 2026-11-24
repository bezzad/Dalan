// The login screen: one card like ngrok (CLI-36). GitHub (AUTH-27), email code (AUTH-24, AUTH-25), SMS code (AUTH-11, AUTH-12),
// password (AUTH-14) and the development login (AUTH-06).
import { ApiError, api, type AuthSettings, type User } from "./api.ts";
import { errorText } from "./app.ts";
import { latinDigits } from "./digits.ts";
import { h } from "./dom.ts";
import type { Language, TextKey } from "./i18n.ts";
import { solve } from "./pow.ts";
import { readSmsCode } from "./webOtp.ts";

type Channel = "sms" | "email";
type Mode = Channel | "password" | "dev";

/** CLI-36: what the first card shows. GitHub first when it is on; then email, or SMS when email is off. */
export function loginLayout(settings: AuthSettings): { github: boolean; first: Channel } {
  return { github: settings.github_login, first: settings.email_login ? "email" : "sms" };
}

/** The GitHub mark, inline: nothing is loaded from another host. */
function githubMark(): SVGSVGElement {
  const ns = "http://www.w3.org/2000/svg";
  const svg = document.createElementNS(ns, "svg");
  svg.setAttribute("viewBox", "0 0 16 16");
  svg.setAttribute("aria-hidden", "true");
  const path = document.createElementNS(ns, "path");
  path.setAttribute("fill", "currentColor");
  path.setAttribute("d", "M8 0c4.42 0 8 3.58 8 8a8.013 8.013 0 0 1-5.45 7.59c-.4.08-.55-.17-.55-.38 0-.27.01-1.13.01-2.2 0-.75-.25-1.23-.54-1.48 1.78-.2 3.65-.88 3.65-3.95 0-.88-.31-1.59-.82-2.15.08-.2.36-1.02-.08-2.12 0 0-.67-.22-2.2.82-.64-.18-1.32-.27-2-.27-.68 0-1.36.09-2 .27-1.53-1.03-2.2-.82-2.2-.82-.44 1.1-.16 1.92-.08 2.12-.51.56-.82 1.28-.82 2.15 0 3.06 1.86 3.75 3.64 3.95-.23.2-.44.55-.51 1.07-.46.21-1.61.55-2.33-.66-.15-.24-.6-.83-1.23-.82-.67.01-.27.38.01.53.34.19.73.9.82 1.13.16.45.68 1.31 2.69.94 0 .67.01 1.3.01 1.49 0 .21-.15.45-.55.38A7.995 7.995 0 0 1 0 8c0-4.42 3.58-8 8-8Z");
  svg.append(path);
  return svg;
}

export function loginScreen(
  language: Language, settings: AuthSettings, onLogin: (user: User) => void, onLanguage: () => void, onBack: () => void,
  firstError: TextKey | null = null): HTMLElement {
  const t = language.t.bind(language);
  const layout = loginLayout(settings);
  const body = h("div", { class: "card-body" });
  const links = h("p", { class: "links" });

  /** A form with an optional title and note, fields and one button. `action` runs on submit; a failed call shows its text. */
  function form(title: TextKey | null, note: TextKey | null, button: TextKey, fields: (Node | string)[], action: () => Promise<void>): HTMLElement {
    const error = h("p", { class: "error", role: "alert" });
    const submit = h("button", { type: "submit", class: "primary" }, t(button));
    return h("form", {
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
    },
    ...(title ? [h("h2", {}, t(title))] : []),
    ...(note ? [h("p", { class: "note" }, t(note))] : []),
    ...fields, error, submit);
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
    const first = channel === layout.first;
    return form(first ? null : way.title, first ? null : way.note, "sendCode", [h("label", { for: target.id }, t(way.label)), target, working], async () => {
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

    // CLI-59: Android Chrome reads the code from the SMS; the user can still type it.
    const reading = new AbortController();
    const view = form(way.title, way.sent, "login", [
      h("p", { dir: "ltr" }, to),
      h("label", { for: "code" }, t("code")), code,
      nameLabel, name,
      h("button", { type: "button", class: "link", onclick: () => open(channel) }, t(way.change)),
    ], async () => {
      reading.abort();
      try {
        onLogin(await way.verify(to, latinDigits(code.value), name.disabled ? undefined : name.value));
      } catch (failure) {
        if (!(failure instanceof ApiError && failure.code === "name_required")) throw failure;
        nameLabel.hidden = name.hidden = name.disabled = false;
        name.focus();
      }
    });
    if (channel === "sms") {
      endings.set(view, () => reading.abort());
      void readSmsCode(reading.signal).then(sms => {
        if (!sms || reading.signal.aborted) return;
        code.value = latinDigits(sms);
        (view as HTMLFormElement).requestSubmit();
      });
    }
    return view;
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

  /** The GitHub button and the "or" line, on the first card only. A plain link: the server sends the browser to GitHub. */
  const github = () => layout.github
    ? [h("a", { class: "button github", href: "/api/auth/github" }, githubMark(), t("byGitHub")), h("p", { class: "or" }, t("orLine"))]
    : [];

  /** CLI-59: what a screen still waits for (the SMS code request), ended when another screen comes. */
  const endings = new WeakMap<HTMLElement, () => void>();
  let leave = (): void => {};

  function show(form: HTMLElement, top: Node[] = []): void {
    leave();
    leave = endings.get(form) ?? (() => {});
    body.replaceChildren(...top, form);
    form.querySelector("input")?.focus();
  }

  function open(mode: Mode, error: TextKey | null = null): void {
    const view = mode === "sms" || mode === "email" ? askCode(mode) : mode === "password" ? password() : dev();
    if (error) view.querySelector(".error")!.textContent = t(error);
    show(view, mode === layout.first ? github() : []);
    const link = (to: Mode, text: TextKey) => h("button", { type: "button", class: "link", onclick: () => open(to) }, t(text));
    links.replaceChildren(
      ...(mode === "email" || !settings.email_login ? [] : [link("email", "byEmail")]),
      ...(mode === "sms" ? [] : [link("sms", "bySms")]),
      ...(mode === "password" ? [] : [link("password", "byPassword")]),
      ...(mode === "dev" || !settings.dev_login ? [] : [link("dev", "byDevLogin")]));
  }

  open(layout.first, firstError);
  return h("main", { class: "login" },
    h("h1", { class: "brand" }, t("appName")),
    h("section", { class: "card" }, body, links),
    h("p", { class: "links" },
      h("button", { type: "button", class: "link", onclick: onLanguage }, t("language")),
      h("button", { type: "button", class: "link", onclick: onBack }, t("back"))));
}
