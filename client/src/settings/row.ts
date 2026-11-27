// CLI-77: the pieces of a settings page: a row (what it is on the start side, the control on the end side), a row with a form
// whose button is primary only while the value changed, a status badge and a login method card. See docs/UI.md.
import { errorText, type App } from "../app.ts";
import { h } from "../dom.ts";
import { glyph, type GlyphName } from "../icon.ts";
import type { TextKey } from "../i18n.ts";
import { toast } from "../toast.ts";
import { changedValue } from "./model.ts";

/** A row: the title and its help on the start side, the controls on the end side. */
export function settingRow(title: string, help: string, ...controls: (Node | string)[]): HTMLElement {
  return h("div", { class: "setting-row" },
    h("div", { class: "what" }, h("h3", {}, title), h("p", { class: "note" }, help)),
    h("div", { class: "control" }, ...controls));
}

/** "Connected" (green) or "Not connected" (grey). */
export function badge(on: boolean, t: (key: TextKey) => string): HTMLElement {
  return h("span", { class: on ? "badge ok" : "badge off" }, t(on ? "connected" : "notConnected"));
}

export interface RowForm {
  element: HTMLFormElement;
  button: HTMLButtonElement;
  /** The values now are the saved ones: the button turns plain again. */
  settle(): void;
}

export interface RowFormOptions {
  title: string;
  help: string;
  /** The fields and notes of the control side, in order. */
  controls: (Node | string)[];
  /** The inputs whose change turns the button primary. */
  inputs: HTMLInputElement[];
  label: string;
  /** Returns the toast text, or nothing for "Saved". */
  action: () => Promise<TextKey | void>;
}

/** CLI-77: a row that is a form. Its button is plain; it is primary only while an input differs from the saved value. */
export function rowForm(app: App, options: RowFormOptions): RowForm {
  const t = app.language.t.bind(app.language);
  const button = h("button", { type: "submit" }, options.label);
  let saved = options.inputs.map(input => input.value);
  const update = () => button.classList.toggle("primary", options.inputs.some((input, i) => changedValue(saved[i] ?? "", input.value)));
  for (const input of options.inputs) input.addEventListener("input", update);
  const settle = () => {
    saved = options.inputs.map(input => input.value);
    update();
  };
  const element = h("form", {
    class: "setting-row",
    onsubmit: async event => {
      event.preventDefault();
      button.disabled = true;
      try {
        const text = (await options.action()) ?? "saved";
        settle();
        toast(t(text));
      } catch (failure) {
        toast(t(errorText(failure)), "error");
      }
      button.disabled = false;
    },
  },
  h("div", { class: "what" }, h("h3", {}, options.title), h("p", { class: "note" }, options.help)),
  h("div", { class: "control" }, ...options.controls, button));
  return { element, button, settle };
}

/** CLI-77: one card per way to log in: icon, name and badge, the one action at the end, details under them. */
export function methodCard(name: string, icon: GlyphName, connected: boolean, t: (key: TextKey) => string, action: HTMLElement | null, detail: string | null): HTMLElement {
  return h("section", { class: "method card" },
    h("div", { class: "head" },
      glyph(icon), h("h3", {}, name), badge(connected, t), ...(action ? [h("span", { class: "grow" }), action] : [])),
    ...(detail ? [h("p", { class: "detail", dir: "auto" }, detail)] : []));
}
