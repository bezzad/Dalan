// The account screen: set the username (AUTH-15), the password (AUTH-13), notifications (CLI-21), building bots (CLI-23),
// the mobile number of a level 1 user (CLI-34), and linking an email or GitHub and merging two accounts (CLI-70).
import { ApiError, api, type MergePlan } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { latinDigits, phoneNumber } from "./digits.ts";
import { h } from "./dom.ts";
import { toast } from "./toast.ts";
import type { TextKey } from "./i18n.ts";
import { mergeRefusal, mergeSides, mergeTitle } from "./merge.ts";
import { builderView } from "./panels.ts";
import { solve } from "./pow.ts";
import { currentPushState, turnOff, turnOn, type PushState } from "./push.ts";

/** CLI-70: the proof found another account of the person (AUTH-26, AUTH-30). */
function isMergeNeeded(failure: unknown): boolean {
  return failure instanceof ApiError && failure.code === "merge_needed";
}

export function accountScreen(app: App): Screen {
  const t = app.language.t.bind(app.language);

  /** A small form. The result ("saved", or the text the action returns) or the error is a toast. */
  function form(fields: (Node | string)[], action: () => Promise<TextKey | void>): HTMLFormElement {
    const submit = h("button", { type: "submit", class: "primary" }, t("save"));
    return h("form", {
      class: "card",
      onsubmit: async event => {
        event.preventDefault();
        submit.disabled = true;
        try {
          toast(t((await action()) ?? "saved"));
        } catch (failure) {
          toast(t(errorText(failure)), "error");
        }
        submit.disabled = false;
      },
    }, ...fields, submit);
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

  // CLI-21: hidden until the server says it has a push key.
  const pushNote = h("p", { class: "note" });
  const pushButton = h("button", { type: "button", class: "primary", hidden: true });
  const pushCard = h("section", { class: "card", hidden: true },
    h("h2", {}, t("notifications")), pushNote, pushButton);
  let pushKey: string | null = null;
  let pushNow: PushState = "hidden";

  function showPush(state: PushState): void {
    pushNow = state;
    pushCard.hidden = state === "hidden";
    pushButton.hidden = state === "unsupported" || state === "blocked";
    pushButton.textContent = t(state === "on" ? "notificationsOff" : "notificationsOn");
    pushNote.textContent = t(state === "on" ? "notificationsAreOn" : state === "unsupported" ? "pushUnsupported"
      : state === "blocked" ? "pushBlocked" : "notificationsNote");
  }

  pushButton.addEventListener("click", async () => {
    pushButton.disabled = true;
    try {
      if (pushNow === "on") {
        await turnOff();
        showPush("off");
      } else {
        showPush(await turnOn(pushKey!));
      }
    } catch (failure) {
      toast(t(errorText(failure)), "error");
    }
    pushButton.disabled = false;
  });

  api.pushKey().then(async key => {
    pushKey = key;
    showPush(await currentPushState(key));
  }).catch(() => {});

  // CLI-34: a level 1 user verifies a mobile number with an SMS code (AUTH-26). Step 1 sends the code, step 2 checks it.
  const phone = h("input", {
    id: "phone", type: "tel", inputmode: "tel", dir: "ltr", autocomplete: "tel", placeholder: "09121234567", required: true });
  const phoneCode = h("input", {
    id: "phone-code", type: "text", inputmode: "numeric", dir: "ltr", autocomplete: "one-time-code", hidden: true });
  const phoneCodeLabel = h("label", { for: "phone-code", hidden: true }, t("code"));
  let sentTo: string | null = null;
  const phoneForm = form([
    h("h2", {}, t("verifyPhone")), h("p", { class: "note" }, t("verifyPhoneNote")),
    h("label", { for: "phone" }, t("phone")), phone, phoneCodeLabel, phoneCode,
  ], async () => {
    if (sentTo === null) {
      const number = phoneNumber(phone.value);
      const { challenge, difficulty } = await api.challenge({ phone: number });
      await api.sendCode(number, challenge, await solve(challenge, difficulty));
      sentTo = number;
      phone.disabled = true;
      phoneCodeLabel.hidden = phoneCode.hidden = false;
      phoneCode.required = true;
      phoneButton.textContent = t("save");
      phoneCode.focus();
      return "codeSent";
    }
    try {
      app.user = await api.addPhone(sentTo, latinDigits(phoneCode.value));
    } catch (failure) {
      if (!isMergeNeeded(failure)) throw failure;
      await showMerge();
      return "mergeNeeded";
    }
    phoneForm.hidden = true;
    showBuilder();
  });
  const phoneButton = phoneForm.querySelector("button[type=submit]")!;
  phoneButton.textContent = t("sendCode");
  phoneForm.hidden = app.user.has_phone;

  // CLI-70: add an email address (AUTH-30). Step 1 sends the code, step 2 checks it.
  const emailInput = h("input", {
    id: "email", type: "email", dir: "ltr", autocomplete: "email", placeholder: "sara@example.com", required: true });
  const emailCode = h("input", {
    id: "email-code", type: "text", inputmode: "numeric", dir: "ltr", autocomplete: "one-time-code", hidden: true });
  const emailCodeLabel = h("label", { for: "email-code", hidden: true }, t("code"));
  let emailSentTo: string | null = null;
  const emailForm = form([
    h("h2", {}, t("addEmail")), h("p", { class: "note" }, t("addEmailNote")),
    h("label", { for: "email" }, t("email")), emailInput, emailCodeLabel, emailCode,
  ], async () => {
    if (emailSentTo === null) {
      const address = emailInput.value.trim();
      const { challenge, difficulty } = await api.challenge({ email: address });
      await api.sendEmailCode(address, challenge, await solve(challenge, difficulty));
      emailSentTo = address;
      emailInput.disabled = true;
      emailCodeLabel.hidden = emailCode.hidden = false;
      emailCode.required = true;
      emailButton.textContent = t("save");
      emailCode.focus();
      return "emailCodeSent";
    }
    try {
      app.user = await api.addEmail(emailSentTo, latinDigits(emailCode.value));
    } catch (failure) {
      if (!isMergeNeeded(failure)) throw failure;
      await showMerge();
      return "mergeNeeded";
    }
    emailForm.hidden = true;
  });
  const emailButton = emailForm.querySelector("button[type=submit]")!;
  emailButton.textContent = t("sendCode");
  emailForm.hidden = app.user.email !== null;

  // CLI-70: link GitHub (AUTH-30). The way back from GitHub opens "/account", or "/account?merge=1" for a merge.
  const githubCard = h("section", { class: "card" },
    h("h2", {}, t("linkGithub")), h("p", { class: "note" }, t("linkGithubNote")),
    h("a", { class: "button primary", href: "/api/account/github" }, t("linkGithub")));
  githubCard.hidden = !app.auth.github_login || app.user.has_github;

  // CLI-70: the merge question (AUTH-31). "Merge" logs the person in as the kept account, so the page loads again.
  const mergeCard = h("section", { class: "card merge" });
  mergeCard.hidden = true;
  async function showMerge(): Promise<void> {
    let plan: MergePlan;
    try {
      plan = await api.mergePlan();
    } catch (failure) {
      toast(t(errorText(failure)), "error");
      return;
    }
    const refusal = mergeRefusal(plan, t);
    const mergeButton = h("button", {
      type: "button", class: "primary", onclick: async () => {
        mergeButton.disabled = true;
        try {
          await api.merge();
          toast(t("mergeDone"));
          window.location.assign("/account");
        } catch (failure) {
          toast(t(errorText(failure)), "error");
          mergeButton.disabled = false;
        }
      },
    }, t("merge"));
    const cancelButton = h("button", {
      type: "button", class: "link", onclick: async () => {
        await api.dropMerge().catch(() => {});
        mergeCard.hidden = true;
      },
    }, t("cancel"));
    mergeCard.replaceChildren(
      h("h2", {}, mergeTitle(plan, t)),
      ...mergeSides(plan, t).map(side => h("p", { dir: "auto" }, h("strong", {}, side.title), " · ", side.counts, " · ", side.fate)),
      h("p", { class: "note" }, refusal ?? t("mergeNote")),
      h("div", { class: "row" }, ...(refusal ? [] : [mergeButton]), cancelButton));
    mergeCard.hidden = false;
    mergeCard.scrollIntoView();
  }

  // Back from GitHub: a merge to answer, or a link that was refused.
  const query = new URLSearchParams(window.location.search);
  if (query.get("merge") === "1") void showMerge();
  if (query.get("link_error") === "has_github") toast(t("hasGithub"), "error");
  if (query.has("merge") || query.has("link_error")) history.replaceState(null, "", "/account");

  // CLI-23: ask to become a builder, wait for the admin, or open "My bots". Admins also open the admin screen.
  const builderBody = h("div", { class: "column" });
  function showBuilder(): void {
    const view = builderView(app.user);
    builderBody.replaceChildren(
      ...(view === "refused" ? [h("p", { class: "note" }, t("builderRefused"))] : []),
      view === "request" || view === "refused"
        ? h("button", {
          type: "button", class: "primary", onclick: async () => {
            try {
              app.user = await api.askBuilder();
              showBuilder();
            } catch (failure) {
              toast(t(errorText(failure)), "error");
            }
          },
        }, t("askBuilder"))
        : view === "phone"
          ? h("p", { class: "note" }, t("phoneRequired"))
        : view === "waiting"
          ? h("p", { class: "note" }, t("builderWaiting"))
          : h("button", { type: "button", class: "primary", onclick: () => app.go("/bots") }, t("myBots")),
      ...(app.user.admin ? [h("button", { type: "button", onclick: () => app.go("/admin") }, t("adminScreen"))] : []));
  }
  showBuilder();
  const builderCard = h("section", { class: "card" },
    h("h2", {}, t("building")), h("p", { class: "note" }, t("buildingNote")), builderBody);

  const element = h("main", { class: "wide cols" },
    h("header", {},
      h("button", { type: "button", class: "link", onclick: () => app.go("/chats") }, t("back")),
      h("h1", {}, t("account"))),
    h("p", { class: "note", dir: "auto" }, app.user.name),
    h("div", { class: "scroller" }, mergeCard, pushCard, phoneForm, emailForm, githubCard, usernameForm, passwordForm, builderCard));

  return { element };
}
