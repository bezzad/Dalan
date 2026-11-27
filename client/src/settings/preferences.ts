// CLI-77: the Preferences section: theme (CLI-35) and language (CLI-04) as radio cards. A choice applies at once, like the header buttons.
import type { App } from "../app.ts";
import { h } from "../dom.ts";
import type { Lang } from "../i18n.ts";
import type { ThemeName } from "../theme.ts";
import { settingRow } from "./row.ts";

/** A card with a radio, a small picture and a name; the chosen one has the brand border. */
function radioCard(group: string, value: string, chosen: boolean, picture: HTMLElement, name: string, choose: () => void): HTMLElement {
  const radio = h("input", { type: "radio", name: group, value, checked: chosen });
  radio.addEventListener("change", choose);
  return h("label", { class: chosen ? "radio-card chosen" : "radio-card" }, radio, picture, h("span", { class: "name" }, name));
}

export function preferencesSection(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  const themes = (["light", "dark"] as const satisfies readonly ThemeName[]).map(name =>
    radioCard("theme", name, app.theme.current === name, h("span", { class: `swatch ${name}`, "aria-hidden": "true" }, h("i", {}), h("i", {})),
      t(name === "light" ? "themeLightName" : "themeDarkName"), () => {
        app.theme.choose(name);
        app.refresh();
      }));
  const languages = ([["fa", "فارسی"], ["en", "English"]] as const satisfies readonly (readonly [Lang, string])[]).map(([lang, name]) =>
    radioCard("language", lang, app.language.current === lang, h("span", { class: "swatch word", lang, "aria-hidden": "true" }, lang === "fa" ? "آ" : "A"),
      name, () => {
        if (app.language.current !== lang) app.language.toggle();
        app.refresh();
      }));
  return h("div", { class: "rows" },
    settingRow(t("themeLabel"), t("themeHelp"), h("div", { class: "radio-cards" }, ...themes)),
    settingRow(t("languageLabel"), t("languageHelp"), h("div", { class: "radio-cards" }, ...languages)));
}
