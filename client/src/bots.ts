// The list of the user's bots, and a field to open a bot by its username.
import { api } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { h } from "./dom.ts";

export function botsScreen(app: App, onLanguage: () => void): Screen {
  const t = app.language.t.bind(app.language);
  const list = h("ul", { class: "bots" });
  const error = h("p", { class: "error", role: "alert" });
  const username = h("input", { id: "bot", type: "text", dir: "ltr", autocapitalize: "none", autocomplete: "off", placeholder: "@bot", "aria-label": t("botUsername"), required: true });

  const open = h("form", {
    class: "row",
    onsubmit: async event => {
      event.preventDefault();
      error.textContent = "";
      try {
        const bot = await api.findBot(username.value.trim().replace(/^@/, ""));
        app.go(`/@${bot.username}`);
      } catch (failure) {
        error.textContent = t(errorText(failure));
      }
    },
  }, username, h("button", { type: "submit", class: "primary" }, t("open")));

  api.bots().then(bots => {
    if (bots.length === 0) list.append(h("li", { class: "note" }, t("noBots")));
    for (const bot of bots) {
      list.append(h("li", {},
        h("a", {
          href: `/@${bot.username}`,
          onclick: event => {
            event.preventDefault();
            app.go(`/@${bot.username}`);
          },
        }, h("strong", { dir: "auto" }, bot.name), h("span", { dir: "ltr" }, `@${bot.username}`))));
    }
  }).catch(failure => {
    error.textContent = t(errorText(failure));
  });

  const element = h("main", {},
    h("header", {},
      h("h1", {}, t("myBots")),
      h("button", { type: "button", class: "link", onclick: onLanguage }, t("language")),
      h("button", { type: "button", class: "link", onclick: () => app.logout() }, t("logout"))),
    h("p", { class: "note", dir: "auto" }, app.user.name),
    open,
    error,
    list);

  return { element };
}
