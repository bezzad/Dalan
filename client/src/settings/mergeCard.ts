// CLI-70: the merge question (AUTH-31 to AUTH-34), on the Login and security page. "Merge" logs the person in as the kept account,
// so the page loads again.
import { ApiError, api, type MergePlan } from "../api.ts";
import { errorText, type App } from "../app.ts";
import { latinDigits } from "../digits.ts";
import { h } from "../dom.ts";
import { mergeRefusal, mergeSides, mergeTitle } from "../merge.ts";
import { toast } from "../toast.ts";
import { sectionPath } from "./model.ts";

/** CLI-70: the proof found another account of the person (AUTH-26, AUTH-30). */
export function isMergeNeeded(failure: unknown): boolean {
  return failure instanceof ApiError && failure.code === "merge_needed";
}

export function mergeCard(app: App): { element: HTMLElement; show(): Promise<void> } {
  const t = app.language.t.bind(app.language);
  const element = h("section", { class: "card merge" });
  element.hidden = true;

  async function show(): Promise<void> {
    let plan: MergePlan;
    try {
      plan = await api.mergePlan();
    } catch (failure) {
      toast(t(errorText(failure)), "error");
      return;
    }
    const refusal = mergeRefusal(plan, t);
    // SEC-15: one side is an admin: the admin's code from the authenticator app, or a recovery code.
    const code = plan.needs_code ? h("input", {
      id: "merge-code", type: "text", inputmode: "numeric", dir: "ltr", autocomplete: "one-time-code", maxlength: "32" }) : null;
    const codeBox = code ? [h("p", { class: "note" }, t("mergeCodeNote")), h("label", { for: "merge-code" }, t("mergeCode")), code] : [];
    const mergeButton = h("button", {
      type: "button", class: "primary", onclick: async () => {
        mergeButton.disabled = true;
        try {
          await api.merge(code ? latinDigits(code.value.trim()) : undefined);
          toast(t("mergeDone"));
          window.location.assign(sectionPath("security"));
        } catch (failure) {
          toast(t(errorText(failure)), "error");
          mergeButton.disabled = false;
        }
      },
    }, t("merge"));
    const cancelButton = h("button", {
      type: "button", class: "link", onclick: async () => {
        await api.dropMerge().catch(() => {});
        element.hidden = true;
      },
    }, t("cancel"));
    element.replaceChildren(
      h("h2", {}, mergeTitle(plan, t)),
      ...mergeSides(plan, t).map(side => h("p", { dir: "auto" }, h("strong", {}, side.title), " · ", side.counts, " · ", side.fate)),
      h("p", { class: "note" }, refusal ?? t("mergeNote")),
      ...(refusal ? [] : codeBox),
      h("div", { class: "row" }, ...(refusal ? [] : [mergeButton]), cancelButton));
    element.hidden = false;
    element.scrollIntoView();
  }

  return { element, show };
}
