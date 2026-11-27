// The account's own care: the sessions list in Login and security (AUTH-35), the "Delete my account" danger card in Profile
// (AUTH-36) and the helpers of the key address editor (AUTH-38). The pure parts are tested in tests/account.test.ts.
import { api, type AccountSession } from "../api.ts";
import { errorText, type App } from "../app.ts";
import { latinDigits } from "../digits.ts";
import { h } from "../dom.ts";
import { solve } from "../pow.ts";
import { toast } from "../toast.ts";
import { settingRow } from "./row.ts";

/** AUTH-35: this device first, then the most recently used. */
export function sortedSessions<T extends { current: boolean; last_used_at: string }>(sessions: readonly T[]): T[] {
  return [...sessions].sort((a, b) => Number(b.current) - Number(a.current) || b.last_used_at.localeCompare(a.last_used_at));
}

/** AUTH-35: "Firefox · Windows", or the given text when the server could not tell. */
export function sessionName(session: { browser: string; system: string }, unknown: string): string {
  const parts = [session.browser, session.system].filter(part => part.trim() !== "");
  return parts.length > 0 ? parts.join(" · ") : unknown;
}

/** AUTH-36: what proves it is the owner: the password, or a fresh code for a user with none. */
export function deleteProof(user: { has_password: boolean }): "password" | "code" {
  return user.has_password ? "password" : "code";
}

/** AUTH-38: the addresses typed in one box: spaces, commas (also the Persian one) or lines between them; repeats go. */
export function parseAddresses(text: string): string[] {
  return [...new Set(latinDigits(text).split(/[\s,،]+/).filter(part => part !== ""))];
}

/** AUTH-35: the live sessions: each can be ended; "Log out other devices" ends all but this one. */
export function sessionsCard(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  const failed = (failure: unknown) => toast(t(errorText(failure)), "error");
  const list = h("ul", { class: "sessions" });
  const others = h("button", { type: "button", hidden: true, onclick: () => {
    if (!confirm(t("endOthersConfirm"))) return;
    api.endOtherSessions().then(() => { toast(t("endOthersDone")); load(); }, failed);
  } }, t("endOthers"));

  function draw(sessions: AccountSession[]): void {
    others.hidden = !sessions.some(session => !session.current);
    list.replaceChildren(...sortedSessions(sessions).map(session => h("li", {},
      h("div", { class: "line" },
        h("strong", { dir: "auto" }, sessionName(session, t("unknownDevice"))),
        ...(session.current ? [h("span", { class: "badge ok" }, t("thisDevice"))] : [])),
      h("span", { class: "note" }, h("bdi", { dir: "ltr" }, session.address), " · ",
        t("lastUsed").replace("{n}", app.language.dateTime(session.last_used_at))),
      ...(session.current ? [] : [h("button", { type: "button", class: "link", onclick: () => {
        api.endSession(session.id).then(load, failed);
      } }, t("endSession"))]))));
  }

  function load(): void {
    api.sessions().then(draw, failed);
  }
  load();
  return h("section", { class: "card column" },
    settingRow(t("sessionsTitle"), t("sessionsHelp"), others), list);
}

/** AUTH-36: the danger card: the word, the password (or a code sent to the own phone or email), then the account is gone. */
export function deleteCard(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  const element = h("section", { class: "card danger column" });
  const failed = (failure: unknown) => toast(t(errorText(failure)), "error");

  function closed(): void {
    element.replaceChildren(h("h3", {}, t("deleteAccount")), h("p", { class: "note" }, t("deleteAccountNote")),
      h("button", { type: "button", onclick: open }, t("deleteAccount")));
  }

  function open(): void {
    const proof = deleteProof(app.user);
    const word = h("input", { id: "delete-word", type: "text", dir: "auto", autocomplete: "off", required: true });
    const secret = proof === "password"
      ? h("input", { id: "delete-proof", type: "password", dir: "ltr", autocomplete: "current-password", required: true })
      : h("input", { id: "delete-proof", type: "text", inputmode: "numeric", dir: "ltr", autocomplete: "one-time-code", required: true });
    const send = h("button", { type: "button", onclick: () => {
      send.disabled = true;
      api.deleteChallenge()
        .then(async ({ challenge, difficulty, channel }) => {
          await api.deleteCode(challenge, await solve(challenge, difficulty));
          toast(t(channel === "email" ? "emailCodeSent" : "codeSent"));
        })
        .catch(failed)
        .finally(() => { send.disabled = false; });
    } }, t("sendCode"));
    const form = h("form", {
      class: "column",
      onsubmit: event => {
        event.preventDefault();
        if (!confirm(t("deleteAccountConfirm"))) return;
        const value = proof === "password" ? secret.value : latinDigits(secret.value.trim());
        api.deleteAccount({ word: word.value.trim(), ...(proof === "password" ? { password: value } : { code: value }) })
          .then(() => { toast(t("accountDeleted")); app.logout(); }, failed);
      },
    },
    h("h3", {}, t("deleteAccount")),
    h("p", { class: "note" }, t("deleteAccountNote")),
    h("label", { for: "delete-word" }, t("deleteWordLabel")), word,
    h("label", { for: "delete-proof" }, t(proof === "password" ? "password" : "code")),
    ...(proof === "code" ? [h("div", { class: "row" }, secret, send)] : [secret]),
    h("div", { class: "row" }, h("button", { type: "submit" }, t("deleteAccount")), h("button", { type: "button", onclick: closed }, t("cancel"))));
    element.replaceChildren(form);
    word.focus();
  }

  closed();
  return element;
}
