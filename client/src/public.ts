// The public pages (CLI-26 to CLI-28): landing, privacy, FAQ and showcase. They open with or without login.
import { api } from "./api.ts";
import { errorText } from "./app.ts";
import { h } from "./dom.ts";
import type { Language } from "./i18n.ts";
import { categoryName, faqItems, initial, landing, privacyPoints, type PublicNumbers, type ShowcaseBot } from "./pages.ts";

export type PublicPage = "landing" | "privacy" | "faq" | "showcase";

export interface PublicContext {
  language: Language;
  loggedIn: boolean;
  /** Goes to an address of the site without a page load. */
  go(path: string): void;
  /** A visitor gets the login screen; a logged-in user goes to the bot list. */
  enter(): void;
  onLanguage(): void;
}

let numbers: Promise<PublicNumbers> | null = null;

export function publicScreen(page: PublicPage, context: PublicContext): HTMLElement {
  const { language } = context;
  const t = language.t.bind(language);
  const error = h("p", { class: "error", role: "alert" });
  const failed = (failure: unknown) => { error.textContent = t(errorText(failure)); };

  const link = (path: string, label: string) => h("a", {
    href: path,
    onclick: event => {
      event.preventDefault();
      context.go(path);
    },
  }, label);

  const nav = h("nav", { class: "links" },
    link("/showcase", t("showcase")), link("/faq", t("faq")), link("/privacy", t("privacy")),
    h("button", { type: "button", class: "link", onclick: context.onLanguage }, t("language")));

  const header = h("header", {},
    h("h1", {}, link("/", t("appName"))),
    h("button", { type: "button", class: "primary", onclick: context.enter }, t(context.loggedIn ? "myBots" : "enter")));

  const body = h("div", { class: "scroller page" });
  const n = (value: number) => language.number(value);

  function withNumbers(draw: (values: PublicNumbers) => void): void {
    (numbers ??= api.publicSettings()).then(draw, failure => {
      numbers = null;
      failed(failure);
    });
  }

  switch (page) {
    case "landing":
      body.append(
        ...landing[language.current].map(message => h("section", { class: "card" }, h("h2", {}, message.title), h("p", {}, message.text))),
        h("section", { class: "card" }, h("h2", {}, t("forBuilders")), h("p", {}, t("forBuildersNote"))),
        h("button", { type: "button", class: "primary", onclick: context.enter }, t(context.loggedIn ? "myBots" : "enter")));
      break;
    case "privacy":
      withNumbers(values => body.append(h("section", { class: "card" }, h("h2", {}, t("privacy")),
        h("ol", {}, ...privacyPoints(language.current, values, n).map(point => h("li", {}, point))))));
      break;
    case "faq":
      withNumbers(values => body.append(...faqItems(language.current, values, n).map(item =>
        h("section", { class: "card" }, h("h2", {}, item.question), h("p", {}, item.answer)))));
      break;
    case "showcase":
      showcase(body);
      break;
  }

  // CLI-28: listed bots, a category filter and "more" while a full page came back.
  function showcase(target: HTMLElement): void {
    const list = h("ul", { class: "bots showcase" });
    const filter = h("nav", { class: "row wrap tabs" });
    const more = h("button", { type: "button", hidden: true }, t("more"));
    let category = "";
    let after: number | undefined;

    const card = (bot: ShowcaseBot) => h("li", {},
      h("a", {
        href: `/@${bot.username}`,
        onclick: event => {
          event.preventDefault();
          context.go(`/@${bot.username}`);
        },
      },
      bot.icon
        ? h("img", { class: "avatar", src: api.showcaseIconUrl(bot.id), alt: "" })
        : h("span", { class: "avatar" }, initial(bot.name)),
      h("span", { class: "column" },
        h("strong", { dir: "auto" }, bot.name),
        h("span", { dir: "ltr" }, `@${bot.username} · ${categoryName(language.current, bot.category)}`),
        h("span", { dir: "auto" }, bot.description))));

    async function load(reset: boolean): Promise<void> {
      if (reset) {
        after = undefined;
        list.replaceChildren();
      }
      const answer = await api.showcase(category, after);
      filter.replaceChildren(...["", ...answer.categories].map(key => h("button", {
        type: "button",
        class: key === category ? "primary" : "",
        onclick: () => {
          category = key;
          load(true).catch(failed);
        },
      }, key ? categoryName(language.current, key) : t("allCategories"))));
      list.append(...answer.bots.map(card));
      if (list.childElementCount === 0) list.append(h("li", { class: "note" }, t("noShowcase")));
      after = answer.bots.at(-1)?.id;
      // An empty page is the end.
      more.hidden = answer.bots.length === 0;
    }
    more.addEventListener("click", () => void load(false).catch(failed));
    target.append(h("p", { class: "note" }, t("showcaseNote")), filter, list, more);
    load(true).catch(failed);
  }

  return h("main", { class: "public" }, header, nav, error, body);
}
