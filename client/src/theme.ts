// CLI-35: the colour theme, light or dark. The first open follows the device; after a press the choice is kept on this device.
import type { LangStorage } from "./i18n.ts";

export type ThemeName = "light" | "dark";

const StorageKey = "dalan.theme";

/** The part of `document.documentElement` that is used: the style sheet reads `data-theme`. */
export interface ThemeRoot {
  dataset: Record<string, string | undefined>;
}

export class Theme {
  private readonly root: ThemeRoot;
  private readonly storage: LangStorage;
  current: ThemeName;

  /** `deviceDark`: the device asks for dark. Used only while the visitor has made no choice. */
  constructor(root: ThemeRoot, storage: LangStorage, deviceDark: boolean) {
    this.root = root;
    this.storage = storage;
    const saved = storage.getItem(StorageKey);
    this.current = saved === "light" || saved === "dark" ? saved : deviceDark ? "dark" : "light";
    this.apply();
  }

  /** Light to dark, or dark to light. The choice is kept. */
  toggle(): void {
    this.current = this.current === "light" ? "dark" : "light";
    this.storage.setItem(StorageKey, this.current);
    this.apply();
  }

  private apply(): void {
    this.root.dataset["theme"] = this.current;
  }
}

const labels = { light: "themeLight", dark: "themeDark" } as const;
// Line icons on a 24 by 24 grid, drawn in the page (rule 7): sun, moon.
const icons: Record<ThemeName, string[]> = {
  light: ["M12 8a4 4 0 1 0 0 8a4 4 0 0 0 0-8z", "M12 2v2", "M12 20v2", "M4.9 4.9l1.4 1.4", "M17.7 17.7l1.4 1.4", "M2 12h2", "M20 12h2", "M4.9 19.1l1.4-1.4", "M17.7 6.3l1.4-1.4"],
  dark: ["M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z"],
};

/** A round button that switches light and dark. It shows the current theme as an icon, and its name to screen readers. */
export function themeButton(theme: Theme, t: (key: (typeof labels)[ThemeName]) => string): HTMLButtonElement {
  const button = document.createElement("button");
  button.type = "button";
  button.className = "theme";
  const draw = () => {
    const ns = "http://www.w3.org/2000/svg";
    const svg = document.createElementNS(ns, "svg");
    svg.setAttribute("viewBox", "0 0 24 24");
    svg.setAttribute("aria-hidden", "true");
    svg.classList.add("icon");
    for (const d of icons[theme.current]) {
      const path = document.createElementNS(ns, "path");
      path.setAttribute("d", d);
      svg.append(path);
    }
    button.replaceChildren(svg);
    button.title = t(labels[theme.current]);
    button.setAttribute("aria-label", button.title);
  };
  const change = () => {
    theme.toggle();
    draw();
  };
  button.addEventListener("click", () => {
    // A wave of the new colours grows from the button. The browser draws it on the GPU; without support, or when the
    // visitor asks for reduced motion, the colours change at once.
    if (!document.startViewTransition || matchMedia("(prefers-reduced-motion: reduce)").matches) return change();
    const box = button.getBoundingClientRect();
    const x = box.left + box.width / 2;
    const y = box.top + box.height / 2;
    const radius = Math.hypot(Math.max(x, innerWidth - x), Math.max(y, innerHeight - y));
    document.startViewTransition(change).ready.then(() => document.documentElement.animate(
      { clipPath: [`circle(0 at ${x}px ${y}px)`, `circle(${radius}px at ${x}px ${y}px)`] },
      { duration: 500, easing: "ease-in-out", pseudoElement: "::view-transition-new(root)" }));
  });
  draw();
  return button;
}
