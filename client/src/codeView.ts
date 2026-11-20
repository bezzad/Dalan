// Code blocks, code tabs and guide steps, shared by the public pages and "My bots" (CLI-26, CLI-38).
import { h } from "./dom.ts";
import type { GuideStep, Sample } from "./guide.ts";

type T = (key: "copy" | "copied") => string;

/** A code block, always left to right, with a copy button. */
export function codeBlock(code: string, t: T): HTMLElement {
  const copy = h("button", {
    type: "button",
    class: "copy",
    onclick: async () => {
      await navigator.clipboard.writeText(code);
      copy.textContent = t("copied");
    },
  }, t("copy"));
  return h("div", { class: "code" }, h("pre", { dir: "ltr" }, h("code", {}, code)), copy);
}

/** Tabs over code blocks: one tab per name, the first one open. */
export function tabbed(samples: Sample[], t: T): HTMLElement[] {
  const tabs = h("div", { class: "os-tabs", role: "tablist" });
  const command = h("div", { class: "command" });
  const pick = (index: number) => {
    for (const [i, tab] of [...tabs.children].entries()) tab.setAttribute("aria-selected", String(i === index));
    command.replaceChildren(codeBlock(samples[index]!.code, t));
  };
  tabs.append(...samples.map((sample, i) => h("button", { type: "button", role: "tab", onclick: () => pick(i) }, sample.name)));
  pick(0);
  return [tabs, command];
}

/** CLI-38: one card per guide step, numbered from `first`. */
export function stepCards(steps: GuideStep[], first: number, t: T, n: (value: number) => string): HTMLElement[] {
  return steps.map((step, i) => h("section", { class: "card" },
    h("h3", {}, `${n(first + i)}. ${step.title}`),
    h("p", {}, step.text),
    ...(step.code ? [codeBlock(step.code, t)] : []),
    ...(step.samples ? tabbed(step.samples, t) : [])));
}
