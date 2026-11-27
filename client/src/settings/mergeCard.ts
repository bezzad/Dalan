// CLI-70: the merge question (AUTH-31 to AUTH-34), on the Login and security page. "Merge" logs the person in as the kept account,
// so the page loads again.
import { ApiError, api, type MergePlan } from "../api.ts";
import { errorText, type App } from "../app.ts";
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
    const mergeButton = h("button", {
      type: "button", class: "primary", onclick: async () => {
        mergeButton.disabled = true;
        try {
          await api.merge();
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
      h("div", { class: "row" }, ...(refusal ? [] : [mergeButton]), cancelButton));
    element.hidden = false;
    element.scrollIntoView();
  }

  return { element, show };
}
