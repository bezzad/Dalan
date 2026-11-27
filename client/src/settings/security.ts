// CLI-77: Login and security: one card per way to log in (password, phone, email, GitHub), the password form, the merge
// question (CLI-70), the second factor (CLI-92) and the sessions (AUTH-35). The way back from GitHub opens it, or "?merge=1" for a merge (AUTH-30, AUTH-31).
import { api } from "../api.ts";
import type { App } from "../app.ts";
import { h } from "../dom.ts";
import type { TextKey } from "../i18n.ts";
import { toast } from "../toast.ts";
import { sessionsCard } from "./account.ts";
import { mergeCard } from "./mergeCard.ts";
import { loginMethods, sectionPath, type MethodAction, type MethodId } from "./model.ts";
import { methodCard, rowForm } from "./row.ts";
import { totpCard } from "./totpCard.ts";

const names: Record<MethodId, TextKey> = { password: "password", phone: "phone", email: "email", github: "github" };
const actions: Record<MethodAction, TextKey> = { setUp: "setUp", change: "change", link: "link" };

export function securitySection(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  const merge = mergeCard(app);
  const element = h("div", { class: "rows" }, merge.element);

  // The current password is asked only when there is one. (After a login by SMS the server does not need it.)
  const current = h("input", { id: "current", type: "password", dir: "ltr", autocomplete: "current-password" });
  const currentLabel = h("label", { for: "current" }, t("currentPassword"));
  const next = h("input", {
    id: "new", type: "password", dir: "ltr", autocomplete: "new-password", minlength: String(app.auth.password_min_length), required: true });
  const passwordRow = rowForm(app, {
    title: t("password"), help: t("passwordNote").replace("{n}", String(app.auth.password_min_length)),
    controls: [currentLabel, current, h("label", { for: "new" }, t("newPassword")), next], inputs: [current, next], label: t("save"),
    action: async () => {
      await api.setPassword(next.value, current.value || undefined);
      app.user = { ...app.user, has_password: true };
      current.value = next.value = "";
      passwordRow.element.hidden = true;
      draw();
    },
  });
  passwordRow.element.hidden = true;

  function action(id: MethodId, kind: MethodAction): HTMLElement {
    const label = t(actions[kind]);
    if (id === "github") return h("a", { class: "button", href: "/api/account/github" }, label);
    return h("button", {
      type: "button", onclick: () => {
        if (id === "password") {
          passwordRow.element.hidden = !passwordRow.element.hidden;
          if (!passwordRow.element.hidden) (app.user.has_password ? current : next).focus();
        } else {
          // The number and the email are set in Profile.
          app.go(sectionPath("profile"));
        }
      },
    }, label);
  }

  function draw(): void {
    currentLabel.hidden = current.hidden = !app.user.has_password;
    element.replaceChildren(merge.element,
      h("div", { class: "methods" }, ...loginMethods(app.user, app.auth.github_login).map(method =>
        methodCard(t(names[method.id]), method.glyph, method.connected, t, method.action ? action(method.id, method.action) : null, method.detail))),
      passwordRow.element,
      // CLI-92, AUTH-37: for a user with a password, or an admin (SEC-15).
      ...(app.user.has_password || app.user.admin ? [secondFactor] : []),
      sessions);
  }
  const sessions = sessionsCard(app);
  const secondFactor = totpCard(app);
  draw();

  // Back from GitHub: a merge to answer, or a link that was refused.
  const query = new URLSearchParams(window.location.search);
  if (query.get("merge") === "1") void merge.show();
  if (query.get("link_error") === "has_github") toast(t("hasGithub"), "error");
  if (query.has("merge") || query.has("link_error")) history.replaceState(null, "", sectionPath("security"));
  return element;
}
