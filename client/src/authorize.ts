// AUTH-41, CLI-97: the consent screen of "Login with Dalan" at /oauth/authorize. The page posts the query as the site sent it;
// the server answers with the question, or with the address back to the site. A visitor logs in first (main.ts, CLI-63).
import { ApiError, api, type Consent } from "./api.ts";
import { errorText, type App } from "./app.ts";
import { h } from "./dom.ts";
import { glyph } from "./icon.ts";
import type { TextKey } from "./i18n.ts";
import { authorizeQuery, consentItems, hostOf } from "./loginGuide.ts";
import { toast } from "./toast.ts";

/** The errors Dalan shows on its own page: nothing goes back to a site that is not sure. */
function refusal(failure: unknown): TextKey {
  if (failure instanceof ApiError && failure.code === "invalid_client") return "consentBadClient";
  if (failure instanceof ApiError && failure.code === "invalid_redirect_uri") return "consentBadReturn";
  return errorText(failure);
}

export function authorizeScreen(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  const query = authorizeQuery(location.search);
  const card = h("section", { class: "card column consent" }, h("p", { class: "note" }, t("loading")));
  const element = h("main", { class: "consent-page" }, card);

  const leave = (redirect: string) => location.assign(redirect);
  const failed = (failure: unknown) => {
    if (failure instanceof ApiError && failure.status === 401) return app.logout();
    card.replaceChildren(
      h("h1", {}, t("consentFailedTitle")),
      h("p", { role: "alert" }, t(refusal(failure))),
      h("div", { class: "row" }, h("button", { type: "button", onclick: () => app.go("/") }, t("home"))));
  };

  function ask(consent: Consent): void {
    const host = hostOf(consent.url);
    const email = h("input", { id: "consent-email", type: "checkbox" });
    const buttons: HTMLButtonElement[] = [];
    const answer = (value: "allow" | "deny") => {
      buttons.forEach(button => { button.disabled = true; });
      api.authorize(query, value, email.checked)
        .then(result => "redirect" in result ? leave(result.redirect) : ask(result.consent))
        .catch(failure => {
          buttons.forEach(button => { button.disabled = false; });
          toast(t(refusal(failure)), "error");
        });
    };
    buttons.push(
      h("button", { type: "button", class: "primary", onclick: () => answer("allow") }, t("consentAllow")),
      h("button", { type: "button", onclick: () => answer("deny") }, t("consentDeny")));
    const items = consentItems(consent.scopes).map(key => h("li", {}, glyph(key === "consentName" ? "profile" : "key"),
      h("span", {}, key === "consentName" ? t(key).replace("{name}", consent.name) : t(key))));
    card.replaceChildren(
      h("h1", {}, t("consentTitle").replace("{site}", "⁨" + host + "⁩")),
      h("p", { class: "note" }, t("consentAs").replace("{name}", consent.name),
        ...(consent.username ? [" ", h("bdi", { dir: "ltr" }, `@${consent.username}`)] : [])),
      h("p", {}, t("consentGets")),
      h("ul", { class: "ticks" }, ...items),
      ...(consent.ask_email && consent.email ? [h("label", { class: "check", for: "consent-email" }, email,
        h("span", {}, t("consentEmail").replace("{email}", "⁨" + consent.email + "⁩")))] : []),
      h("p", { class: "note" }, t("consentNoPhone")),
      h("div", { class: "row" }, ...buttons));
  }

  api.authorize(query, null, false)
    .then(result => "redirect" in result ? leave(result.redirect) : ask(result.consent))
    .catch(failed);
  return element;
}
