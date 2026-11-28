// The toast (docs/UI.md): a short note at the bottom corner after every action the user takes. It goes away by itself.
import { h } from "./dom.ts";

export type ToastKind = "ok" | "error" | "info";

/** CLI-98: the most toasts shown at once. On a phone they are as wide as the screen and would cover the menu. */
export const maxToasts = 3;

let region: HTMLElement | null = null;

/** The one place the toasts live. `show()` in main.ts puts it back after each screen change. */
export function toastRegion(): HTMLElement {
  return region ??= h("div", { class: "toasts", "aria-live": "polite" });
}

/**
 * CLI-98: which of the shown toasts (oldest first) go when a toast with `text` comes: one with the same text,
 * then the oldest, until the new one fits under `maxToasts`.
 */
export function toastsToDrop(shown: readonly string[], text: string): number[] {
  const same = shown.flatMap((other, index) => other === text ? [index] : []);
  const left = shown.length - same.length;
  const oldest = shown.flatMap((other, index) => other === text ? [] : [index]).slice(0, Math.max(0, left - (maxToasts - 1)));
  return [...same, ...oldest].sort((a, b) => a - b);
}

export function toast(text: string, kind: ToastKind = "ok"): void {
  const shown = [...toastRegion().children] as HTMLElement[];
  for (const index of toastsToDrop(shown.map(item => item.dataset.text ?? ""), text)) shown[index]!.remove();
  const item = h("div", { class: `toast ${kind}`, role: kind === "error" ? "alert" : "status", "data-text": text },
    h("span", { class: "dot", "aria-hidden": "true" }), h("span", {}, text));
  const remove = () => item.remove();
  item.addEventListener("click", remove);
  toastRegion().append(item);
  if (!toastRegion().isConnected) document.body.append(toastRegion());
  // Errors stay a little longer: the user may need to read them twice.
  window.setTimeout(remove, kind === "error" ? 7000 : 4000);
}
