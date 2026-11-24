// The login screen: one card of buttons, one per way (CLI-36). GitHub (AUTH-27), email code (AUTH-24, AUTH-25), SMS code
// (AUTH-11, AUTH-12), password (AUTH-14) and the development login (AUTH-06). Send again with a countdown (CLI-61).
import { ApiError, api, type AuthSettings, type User } from "./api.ts";
import { errorText } from "./app.ts";
import { latinDigits, phoneNumber } from "./digits.ts";
import { h } from "./dom.ts";
import type { Language, LangStorage, TextKey } from "./i18n.ts";
import { solve } from "./pow.ts";
import { readSmsCode } from "./webOtp.ts";

type Channel = "sms" | "email";
type Way = Channel | "github";
type Mode = Channel | "password" | "dev";

/** CLI-36: set after any login on this browser; it stays after logout. */
const ReturningKey = "dalan.returning";

/** CLI-36: true when this browser logged in before. Storage that fails (private mode) is a new browser. */
export function isReturning(storage?: LangStorage): boolean {
  try {
    return (storage ?? localStorage).getItem(ReturningKey) === "1";
  } catch {
    return false;
  }
}

export function markReturning(storage?: LangStorage): void {
  try {
    (storage ?? localStorage).setItem(ReturningKey, "1");
  } catch {
    // Nothing to keep: the next login screen is the one for a new browser.
  }
}

/** CLI-36: a new person comes for SMS; a returning one is fastest with GitHub or email. Ways that are off are left out. */
export function loginOrder(settings: AuthSettings, returning: boolean): Way[] {
  const order: Way[] = returning ? ["github", "email", "sms"] : ["sms", "github", "email"];
  return order.filter(way => way === "sms" || (way === "github" ? settings.github_login : settings.email_login));
}

/** CLI-61: seconds as m:ss, or h:mm:ss from an hour. */
export function clock(seconds: number): string {
  const s = Math.max(0, Math.ceil(seconds));
  const two = (n: number) => String(n).padStart(2, "0");
  const hours = Math.floor(s / 3600);
  const minutes = Math.floor(s / 60) % 60;
  return hours > 0 ? `${hours}:${two(minutes)}:${two(s % 60)}` : `${Math.floor(s / 60)}:${two(s % 60)}`;
}

/** An inline mark: nothing is loaded from another host. A line drawing unless `filled`. */
function mark(d: string, filled = false): SVGSVGElement {
  const ns = "http://www.w3.org/2000/svg";
  const svg = document.createElementNS(ns, "svg");
  svg.setAttribute("viewBox", "0 0 16 16");
  svg.setAttribute("aria-hidden", "true");
  const path = document.createElementNS(ns, "path");
  if (filled) {
    path.setAttribute("fill", "currentColor");
  } else {
    path.setAttribute("fill", "none");
    path.setAttribute("stroke", "currentColor");
    path.setAttribute("stroke-width", "1.5");
    path.setAttribute("stroke-linejoin", "round");
  }
  path.setAttribute("d", d);
  svg.append(path);
  return svg;
}

const githubMark = () => mark("M8 0c4.42 0 8 3.58 8 8a8.013 8.013 0 0 1-5.45 7.59c-.4.08-.55-.17-.55-.38 0-.27.01-1.13.01-2.2 0-.75-.25-1.23-.54-1.48 1.78-.2 3.65-.88 3.65-3.95 0-.88-.31-1.59-.82-2.15.08-.2.36-1.02-.08-2.12 0 0-.67-.22-2.2.82-.64-.18-1.32-.27-2-.27-.68 0-1.36.09-2 .27-1.53-1.03-2.2-.82-2.2-.82-.44 1.1-.16 1.92-.08 2.12-.51.56-.82 1.28-.82 2.15 0 3.06 1.86 3.75 3.64 3.95-.23.2-.44.55-.51 1.07-.46.21-1.61.55-2.33-.66-.15-.24-.6-.83-1.23-.82-.67.01-.27.38.01.53.34.19.73.9.82 1.13.16.45.68 1.31 2.69.94 0 .67.01 1.3.01 1.49 0 .21-.15.45-.55.38A7.995 7.995 0 0 1 0 8c0-4.42 3.58-8 8-8Z", true);
const smsMark = () => mark("M2.5 2.5h11v8h-7l-3 3v-3h-1z");
const emailMark = () => mark("M1.5 3.5h13v9h-13zM1.5 3.5 8 9l6.5-5.5");

export function loginScreen(
  language: Language, settings: AuthSettings, onLogin: (user: User) => void, onLanguage: () => void, onBack: () => void,
  firstError: TextKey | null = null): HTMLElement {
  const t = language.t.bind(language);
  const body = h("div", { class: "card-body" });
  const links = h("p", { class: "links" });

  /** CLI-61: a 429 with Retry-After says how long to wait; never "tomorrow". */
  function failureText(failure: unknown): string {
    return failure instanceof ApiError && failure.status === 429 && failure.retryAfter > 0
      ? t("tryAgainIn").replace("{time}", clock(failure.retryAfter))
      : t(errorText(failure));
  }

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
          error.textContent = failureText(failure);
        }
        submit.disabled = false;
      },
    },
    ...(title ? [h("h2", {}, t(title))] : []),
    ...(note ? [h("p", { class: "note" }, t(note))] : []),
    ...fields, error, submit);
  }

  const backButton = () => h("button", { type: "button", class: "link", onclick: () => choices() }, t("back"));

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
      input: phoneInput, clean: phoneNumber,
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

  /** CLI-36: the first card: one equal button per way, in the order for this browser. */
  function choices(error: TextKey | null = null): void {
    const buttons = loginOrder(settings, isReturning()).map(way => way === "github"
      ? h("a", { class: "button way github", href: "/api/auth/github" }, githubMark(), t("byGitHub"))
      : h("button", { type: "button", class: `button way ${way}`, onclick: () => open(way) },
        way === "sms" ? smsMark() : emailMark(), t(way === "sms" ? "bySms" : "byEmail")));
    const view = h("div", { class: "ways" },
      h("p", { class: "error", role: "alert" }, error ? t(error) : ""), ...buttons);
    show(view);
    bottomLinks(null);
  }

  /** Step 1: the phone or email. The browser does the proof of work (CLI-10), then the server sends the code. */
  function askCode(channel: Channel): HTMLElement {
    const way = channels[channel];
    const target = way.input();
    const working = h("p", { class: "note", role: "status" });
    return form(way.title, way.note, "sendCode", [h("label", { for: target.id }, t(way.label)), target, working, backButton()], async () => {
      const to = way.clean(target.value);
      working.textContent = t("preparing");
      try {
        const sent = await way.ask(to);
        show(enterCode(channel, to, sent));
      } finally {
        working.textContent = "";
      }
    });
  }

  /** Step 2: the code. A new phone or email is asked for a name too, and the same code is sent again with it. CLI-33: the email note says "check spam". */
  function enterCode(channel: Channel, to: string, sent: { retry_in: number; code_digits: number }): HTMLElement {
    const way = channels[channel];
    const code = h("input", {
      id: "code", type: "text", inputmode: "numeric", dir: "ltr", autocomplete: "one-time-code", maxlength: String(sent.code_digits), required: true });
    const name = nameInput();
    const nameLabel = h("label", { for: "name", hidden: true }, t(way.newName));
    name.hidden = name.disabled = true;

    // CLI-61: "Send again" is off while the server would refuse it, with the time left on it.
    const again = h("button", { type: "button", class: "link" });
    let timer: ReturnType<typeof setInterval> | undefined;
    function countdown(seconds: number): void {
      clearInterval(timer);
      const end = Date.now() + seconds * 1000;
      const tick = () => {
        const left = (end - Date.now()) / 1000;
        again.disabled = left > 0;
        again.textContent = left > 0 ? `${t("sendAgain")} (${clock(left)})` : t("sendAgain");
        if (left <= 0) clearInterval(timer);
      };
      tick();
      timer = setInterval(tick, 1000);
    }
    again.addEventListener("click", async () => {
      const error = view.querySelector(".error")!;
      error.textContent = "";
      again.disabled = true;
      try {
        countdown((await way.ask(to)).retry_in);
      } catch (failure) {
        error.textContent = failureText(failure);
        countdown(failure instanceof ApiError ? failure.retryAfter : 0);
      }
    });

    // CLI-59: Android Chrome reads the code from the SMS; the user can still type it.
    const reading = new AbortController();
    const view = form(way.title, way.sent, "login", [
      h("p", { dir: "ltr" }, to),
      h("label", { for: "code" }, t("code")), code,
      nameLabel, name,
      again,
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
    countdown(sent.retry_in);
    endings.set(view, () => {
      reading.abort();
      clearInterval(timer);
    });
    if (channel === "sms") {
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
      backButton(),
    ], async () => onLogin(await api.passwordLogin(phoneNumber(login.value), secret.value)));
  }

  function dev(): HTMLElement {
    const phone = phoneInput();
    const name = nameInput();
    return form("devLoginTitle", "devLoginNote", "login", [
      h("label", { for: "phone" }, t("phone")), phone,
      h("label", { for: "name" }, t("name")), name,
      backButton(),
    ], async () => onLogin(await api.devLogin(phoneNumber(phone.value), name.value)));
  }

  /** CLI-59, CLI-61: what a screen still runs (the SMS code request, the countdown), ended when another screen comes. */
  const endings = new WeakMap<HTMLElement, () => void>();
  let leave = (): void => {};

  function show(view: HTMLElement): void {
    leave();
    leave = endings.get(view) ?? (() => {});
    body.replaceChildren(view);
    view.querySelector("input")?.focus();
  }

  /** Password and the development login stay small links at the bottom. */
  function bottomLinks(mode: Mode | null): void {
    const link = (to: Mode, text: TextKey) => h("button", { type: "button", class: "link", onclick: () => open(to) }, t(text));
    links.replaceChildren(
      ...(mode === "password" ? [] : [link("password", "byPassword")]),
      ...(mode === "dev" || !settings.dev_login ? [] : [link("dev", "byDevLogin")]));
  }

  function open(mode: Mode): void {
    show(mode === "sms" || mode === "email" ? askCode(mode) : mode === "password" ? password() : dev());
    bottomLinks(mode);
  }

  choices(firstError);
  return h("main", { class: "login" },
    h("h1", { class: "brand" }, t("appName")),
    h("section", { class: "card" }, body, links),
    h("p", { class: "links" },
      h("button", { type: "button", class: "link", onclick: onLanguage }, t("language")),
      h("button", { type: "button", class: "link", onclick: onBack }, t("back"))));
}
