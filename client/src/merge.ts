// CLI-70: the merge question of the account screen (AUTH-31 to AUTH-34). The texts are pure, so tests run without a browser.
import type { MergePlan, MergeSide } from "./api.ts";
import type { TextKey } from "./i18n.ts";

type Translate = (key: TextKey) => string;

const wayKeys: Record<MergePlan["way"], TextKey> = { phone: "wayPhone", email: "wayEmail", github: "wayGithub" };

/** "This mobile number already has an account. Merge it into this account?" */
export function mergeTitle(plan: MergePlan, t: Translate): string {
  return t("mergeTitle").replace("{way}", t(wayKeys[plan.way]));
}

/** "2 bots, 0 sites, 0 keys, 3 chats" */
export function mergeCounts(side: MergeSide, t: Translate, n: (value: number) => string = String): string {
  return t("mergeCounts")
    .replace("{bots}", n(side.bots)).replace("{sites}", n(side.sites)).replace("{keys}", n(side.keys)).replace("{chats}", n(side.chats));
}

/** Why the merge cannot be done, or null when it can (then the question has a "Merge" button). */
export function mergeRefusal(plan: MergePlan, t: Translate): string | null {
  switch (plan.refusal) {
    case null: return null;
    case "both_have_products": return t("bothHaveProducts");
    case "other_has_products": return t("otherHasProducts");
    case "merge_conflict": return t("mergeConflict").replace("{field}", t(wayKeys[plan.field ?? "phone"]));
    // SEC-15: two admin accounts are never one. One admin side needs the code (plan.needs_code), not a refusal.
    case "admin": return t("adminMergeBoth");
    default: return t("accountSuspended");
  }
}

/** One line per side: whose account, what it has, and whether it stays. */
export function mergeSides(plan: MergePlan, t: Translate, n?: (value: number) => string): { title: string; counts: string; fate: string }[] {
  return (["this", "other"] as const).map(side => ({
    title: `${t(side === "this" ? "mergeThis" : "mergeOther")}: ${plan[side].name}`,
    counts: mergeCounts(plan[side], t, n),
    fate: t(plan.keep === side ? "mergeKept" : "mergeGone"),
  }));
}
