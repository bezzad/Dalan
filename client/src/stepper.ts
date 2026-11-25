// CLI-64, CLI-65: the guide as a stepper. The step to do now is open; a step already done is green and closed.
import { codeBlock, tabbed } from "./codeView.ts";
import { h } from "./dom.ts";
import type { GuideStep } from "./guide.ts";
import type { TextKey } from "./i18n.ts";
import { stepStates, stepsDone } from "./steps.ts";

export interface StepperOptions {
  steps: GuideStep[];
  /** What the server says is done (siteDone, botDone); a step beyond this list is not known. */
  done: boolean[];
  t: (key: TextKey) => string;
  n: (value: number) => string;
  /** CLI-65: opens the key modal; `hasKey` makes the link say "Make another key". */
  makeKey?: (made: () => void) => void;
  hasKey?: boolean;
}

export function stepper(options: StepperOptions): HTMLElement {
  const { steps, t, n } = options;
  // A step the server cannot know is done only when the person presses "Next".
  const pressed = steps.map(() => false);
  let hasKey = options.hasKey ?? false;
  const keyStep = steps.findIndex(step => step.key === true);
  const isDone = () => stepsDone(options.done, pressed, keyStep, hasKey);
  let open = Math.max(0, isDone().indexOf(false) === -1 ? steps.length - 1 : isDone().indexOf(false));
  const list = h("ol", { class: "stepper" });

  function draw(): void {
    const states = stepStates(isDone());
    list.replaceChildren(...steps.map((step, i) => {
      const state = states[i]!;
      const isOpen = i === open;
      return h("li", { class: `card step ${state}${isOpen ? " open" : ""}` },
        h("button", { type: "button", class: "step-head", "aria-expanded": String(isOpen), onclick: () => { open = isOpen ? -1 : i; draw(); } },
          h("span", { class: "step-mark" }, state === "done" ? "✓" : n(i + 1)),
          h("span", { class: "step-title" }, step.title),
          ...(state === "done" ? [h("span", { class: "step-done" }, t("stepDone"))] : [h("span", { class: "step-show" }, isOpen ? "" : t("stepShow"))])),
        ...(isOpen ? [body(step, i)] : []));
    }));
  }

  function body(step: GuideStep, i: number): HTMLElement {
    return h("div", { class: "step-body" },
      h("p", {}, step.text),
      ...(step.code ? [codeBlock(step.code, t)] : []),
      ...(step.samples ? tabbed(step.samples, t) : []),
      ...(step.key && options.makeKey
        ? [h("div", { class: "row" }, h("button", { type: "button", class: hasKey ? "link" : "primary", onclick: () => options.makeKey!(() => { hasKey = true; draw(); }) },
          t(hasKey ? "keyLinkAgain" : "keyLink")))]
        : []),
      h("div", { class: "row" },
        ...(i > 0 ? [h("button", { type: "button", class: "link", onclick: () => { open = i - 1; draw(); } }, t("stepBack"))] : []),
        ...(i < steps.length - 1 ? [h("button", { type: "button", class: "primary", onclick: () => { pressed[i] = true; open = i + 1; draw(); } }, t("stepNext"))] : [])));
  }

  draw();
  return list;
}
