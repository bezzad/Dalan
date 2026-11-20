// The toast (docs/UI.md): a short note at the bottom corner after every action the user takes. It goes away by itself.
import { h } from "./dom.ts";

export type ToastKind = "ok" | "error" | "info";

let region: HTMLElement | null = null;

/** The one place the toasts live. `show()` in main.ts puts it back after each screen change. */
export function toastRegion(): HTMLElement {
  return region ??= h("div", { class: "toasts", "aria-live": "polite" });
}

export function toast(text: string, kind: ToastKind = "ok"): void {
  const item = h("div", { class: `toast ${kind}`, role: kind === "error" ? "alert" : "status" },
    h("span", { class: "dot", "aria-hidden": "true" }), h("span", {}, text));
  const remove = () => item.remove();
  item.addEventListener("click", remove);
  toastRegion().append(item);
  if (!toastRegion().isConnected) document.body.append(toastRegion());
  // Errors stay a little longer: the user may need to read them twice.
  window.setTimeout(remove, kind === "error" ? 7000 : 4000);
}
