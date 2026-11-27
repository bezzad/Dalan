// CLI-77: the Profile section: the username (AUTH-15), the mobile number of a level 1 user (CLI-34) an email (CLI-70) and Delete my account (AUTH-36).
// A number or an email that clashes with another account goes to the merge question on Login and security.
import { api } from "../api.ts";
import type { App } from "../app.ts";
import { latinDigits, phoneNumber } from "../digits.ts";
import { h } from "../dom.ts";
import { solve } from "../pow.ts";
import { deleteCard } from "./account.ts";
import { isMergeNeeded } from "./mergeCard.ts";
import { sectionPath } from "./model.ts";
import { badge, rowForm, settingRow } from "./row.ts";

export function profileSection(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  const element = h("div", { class: "rows" });

  /** AUTH-31: the proof found another account; the merge question is on Login and security. */
  async function toMerge(): Promise<"mergeNeeded"> {
    app.go(`${sectionPath("security")}?merge=1`);
    return "mergeNeeded";
  }

  function username(): HTMLElement {
    const input = h("input", {
      id: "username", type: "text", dir: "ltr", autocapitalize: "none", autocomplete: "username", required: true, value: app.user.username ?? "" });
    return rowForm(app, {
      title: t("username"), help: t("usernameNote"), controls: [input], inputs: [input], label: t("save"),
      action: async () => {
        app.user = await api.setUsername(input.value.trim());
      },
    }).element;
  }

  // CLI-34: a level 1 user verifies a mobile number with an SMS code (AUTH-26). Step 1 sends the code, step 2 checks it.
  function phone(): HTMLElement {
    if (app.user.has_phone) return settingRow(t("phone"), t("phoneSetNote"), badge(true, t));
    const input = h("input", {
      id: "phone", type: "tel", inputmode: "tel", dir: "ltr", autocomplete: "tel", placeholder: "09121234567", required: true });
    const codeLabel = h("label", { for: "phone-code", hidden: true }, t("code"));
    const code = h("input", { id: "phone-code", type: "text", inputmode: "numeric", dir: "ltr", autocomplete: "one-time-code", hidden: true });
    let sentTo: string | null = null;
    const row = rowForm(app, {
      title: t("phone"), help: t("verifyPhoneNote"), controls: [input, codeLabel, code], inputs: [input, code], label: t("sendCode"),
      action: async () => {
        if (sentTo === null) {
          const number = phoneNumber(input.value);
          const { challenge, difficulty } = await api.challenge({ phone: number });
          await api.sendCode(number, challenge, await solve(challenge, difficulty));
          sentTo = number;
          input.disabled = true;
          codeLabel.hidden = code.hidden = false;
          code.required = true;
          row.button.textContent = t("save");
          code.focus();
          return "codeSent";
        }
        try {
          app.user = await api.addPhone(sentTo, latinDigits(code.value));
        } catch (failure) {
          if (!isMergeNeeded(failure)) throw failure;
          return toMerge();
        }
        draw();
      },
    });
    return row.element;
  }

  // CLI-70: add an email address (AUTH-30). Step 1 sends the code, step 2 checks it.
  function email(): HTMLElement {
    if (app.user.email !== null) return settingRow(t("email"), t("emailSetNote"), h("p", { dir: "ltr" }, app.user.email));
    const input = h("input", {
      id: "email", type: "email", dir: "ltr", autocomplete: "email", placeholder: "sara@example.com", required: true });
    const codeLabel = h("label", { for: "email-code", hidden: true }, t("code"));
    const code = h("input", { id: "email-code", type: "text", inputmode: "numeric", dir: "ltr", autocomplete: "one-time-code", hidden: true });
    let sentTo: string | null = null;
    const row = rowForm(app, {
      title: t("email"), help: t("addEmailNote"), controls: [input, codeLabel, code], inputs: [input, code], label: t("sendCode"),
      action: async () => {
        if (sentTo === null) {
          const address = input.value.trim();
          const { challenge, difficulty } = await api.challenge({ email: address });
          await api.sendEmailCode(address, challenge, await solve(challenge, difficulty));
          sentTo = address;
          input.disabled = true;
          codeLabel.hidden = code.hidden = false;
          code.required = true;
          row.button.textContent = t("save");
          code.focus();
          return "emailCodeSent";
        }
        try {
          app.user = await api.addEmail(sentTo, latinDigits(code.value));
        } catch (failure) {
          if (!isMergeNeeded(failure)) throw failure;
          return toMerge();
        }
        draw();
      },
    });
    return row.element;
  }

  function draw(): void {
    // AUTH-36: an admin comes from the server's environment and cannot delete the account here.
    element.replaceChildren(username(), phone(), email(), ...(app.user.admin ? [] : [deleteCard(app)]));
  }
  draw();
  return element;
}
