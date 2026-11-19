// CLI-35: the colour theme. "system" follows the device; "light" and "dark" are the visitor's choice, kept on this device.
import type { LangStorage } from "./i18n.ts";

export type ThemeName = "system" | "light" | "dark";

const StorageKey = "dalan.theme";
const order: ThemeName[] = ["system", "light", "dark"];

/** The part of `document.documentElement` that is used: the style sheet reads `data-theme`. */
export interface ThemeRoot {
  dataset: Record<string, string | undefined>;
}

export class Theme {
  private readonly root: ThemeRoot;
  private readonly storage: LangStorage;
  current: ThemeName;

  constructor(root: ThemeRoot, storage: LangStorage) {
    this.root = root;
    this.storage = storage;
    const saved = storage.getItem(StorageKey);
    this.current = saved === "light" || saved === "dark" ? saved : "system";
    this.apply();
  }

  /** The next theme: system, light, dark, then system again. */
  toggle(): void {
    this.current = order[(order.indexOf(this.current) + 1) % order.length]!;
    this.storage.setItem(StorageKey, this.current);
    this.apply();
  }

  private apply(): void {
    if (this.current === "system") delete this.root.dataset["theme"];
    else this.root.dataset["theme"] = this.current;
  }
}

const labels = { system: "themeSystem", light: "themeLight", dark: "themeDark" } as const;
// Line icons on a 24 by 24 grid, drawn in the page (rule 7): half circle, sun, moon.
const icons: Record<ThemeName, string[]> = {
  system: ["M12 3a9 9 0 1 0 0 18a9 9 0 0 0 0-18z", "M12 3v18", "M12 7h4", "M12 11h6", "M12 15h5"],
  light: ["M12 8a4 4 0 1 0 0 8a4 4 0 0 0 0-8z", "M12 2v2", "M12 20v2", "M4.9 4.9l1.4 1.4", "M17.7 17.7l1.4 1.4", "M2 12h2", "M20 12h2", "M4.9 19.1l1.4-1.4", "M17.7 6.3l1.4-1.4"],
  dark: ["M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z"],
};

/** A round button that moves to the next theme. It shows the current theme as an icon, and its name to screen readers. */
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
  button.addEventListener("click", () => {
    theme.toggle();
    draw();
  });
  draw();
  return button;
}
