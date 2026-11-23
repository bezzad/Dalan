// The list of the user's bots, and a field to open a bot by its username.
import { api } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { h } from "./dom.ts";
import { botFace } from "./icon.ts";

export function botsScreen(app: App): Screen {
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

  // CLI-18: each bot with its unread count.
  function load(): void {
    api.bots().then(bots => {
      list.replaceChildren(...(bots.length === 0 ? [h("li", { class: "note" }, t("noBots"))] : []), ...bots.map(bot =>
        h("li", {},
          h("a", {
            href: `/@${bot.username}`,
            onclick: event => {
              event.preventDefault();
              app.go(`/@${bot.username}`);
            },
          },
          // CLI-50: the bot's icon or its first letter.
          botFace(bot),
          // <bdi>: a Latin name keeps its own direction, but the row still starts at the language's side.
          h("strong", {}, h("bdi", {}, bot.name)),
          h("span", { dir: "ltr" }, `@${bot.username}`),
          ...(bot.unread > 0
            ? [h("span", { class: "badge", title: t("unread").replace("{n}", app.language.number(bot.unread)) }, app.language.number(bot.unread))]
            : [])))));
    }).catch(failure => {
      error.textContent = t(errorText(failure));
    });
  }
  load();

  // CLI-18: a new message or a read on another device loads the list again, at most once a second.
  let timer = 0;
  const reload = () => {
    if (timer === 0) timer = window.setTimeout(() => { timer = 0; load(); }, 1000);
  };

  const element = h("main", {},
    h("header", {},
      h("h1", {}, t("chats"))),
    h("p", { class: "note" }, h("bdi", {}, app.user.name)),
    ...(app.user.has_password ? [] : [h("p", { class: "note" }, t("noPasswordYet"))]),
    open,
    // CLI-24: one press away. My bots shows a builder its bots, and anyone else the next step to become one.
    h("button", { type: "button", class: "primary", onclick: () => app.go("/bots") }, t("createBot")),
    error,
    list,
    // CLI-26: the public pages, from inside the app too.
    h("nav", { class: "links" }, ...([["", "home"], ["showcase", "showcase"], ["faq", "faq"], ["privacy", "privacy"]] as const).map(([path, label]) => h("a", {
      href: `/${path}`,
      onclick: event => {
        event.preventDefault();
        app.go(`/${path}`);
      },
    }, t(label)))));

  return { element, onMessage: reload, onRead: reload };
}
