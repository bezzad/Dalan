// The public pages (CLI-26 to CLI-28): landing, privacy, FAQ and showcase. They open with or without login.
import { api } from "./api.ts";
import { errorText } from "./app.ts";
import { h } from "./dom.ts";
import type { Language } from "./i18n.ts";
import { categoryName, closing, diagram, faqItems, hero, initial, landing, privacyPoints, steps, type PublicNumbers, type ShowcaseBot } from "./pages.ts";

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

// Small line icons, drawn in the page (rule 7: nothing from another host). Each is a list of SVG path data on a 24 by 24 grid.
const icons = {
  phone: ["M7 2h10a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z", "M11 18h2"],
  gate: ["M4 21V10a8 8 0 0 1 16 0v11", "M9 21v-6a3 3 0 0 1 6 0v6", "M2 21h20"],
  computer: ["M3 4h18v12H3z", "M8 20h8", "M12 16v4"],
  home: ["M3 11l9-8 9 8", "M5 10v10h14V10", "M10 20v-5h4v5"],
  shield: ["M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z", "M9 12l2 2 4-4"],
  offline: ["M12 3a9 9 0 1 0 0 18a9 9 0 0 0 0-18z", "M3 12h18", "M4 4l16 16"],
};

function icon(name: keyof typeof icons): SVGSVGElement {
  const ns = "http://www.w3.org/2000/svg";
  const svg = document.createElementNS(ns, "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("aria-hidden", "true");
  svg.classList.add("icon");
  for (const d of icons[name]) {
    const path = document.createElementNS(ns, "path");
    path.setAttribute("d", d);
    svg.append(path);
  }
  return svg;
}

export function publicScreen(page: PublicPage, context: PublicContext): HTMLElement {
  const { language } = context;
  const t = language.t.bind(language);
  const error = h("p", { class: "error", role: "alert" });
  const failed = (failure: unknown) => { error.textContent = t(errorText(failure)); };

  const pageLink = (path: string, label: string, kind = "") => h("a", {
    href: path,
    class: kind,
    onclick: event => {
      event.preventDefault();
      context.go(path);
    },
  }, label);

  const nav = h("nav", { class: "links" },
    pageLink("/showcase", t("showcase")), pageLink("/faq", t("faq")), pageLink("/privacy", t("privacy")),
    h("button", { type: "button", class: "link", onclick: context.onLanguage }, t("language")));

  const enterButton = () => h("button", { type: "button", class: "primary", onclick: context.enter }, t(context.loggedIn ? "myBots" : "enter"));

  const header = h("header", { class: "site" },
    h("h1", {}, h("a", {
      href: "/",
      onclick: event => {
        event.preventDefault();
        context.go("/");
      },
    }, h("span", { class: "mark" }, icon("gate")), t("appName"))),
    nav,
    enterButton());

  const body = h("div", { class: "content" });
  const lang = language.current;
  const n = (value: number) => language.number(value);

  function withNumbers(draw: (values: PublicNumbers) => void): void {
    (numbers ??= api.publicSettings()).then(draw, failure => {
      numbers = null;
      failed(failure);
    });
  }

  switch (page) {
    case "landing":
      landingPage(body);
      break;
    case "privacy":
      withNumbers(values => body.append(h("section", { class: "card" }, h("h2", {}, t("privacy")),
        h("ol", {}, ...privacyPoints(language.current, values, n).map(point => h("li", {}, point))))));
      break;
    case "faq":
      body.append(h("h2", { class: "title" }, t("faq")));
      withNumbers(values => body.append(...faqItems(language.current, values, n).map(item =>
        h("section", { class: "card" }, h("h2", {}, item.question), h("p", {}, item.answer)))));
      break;
    case "showcase":
      body.append(h("h2", { class: "title" }, t("showcase")));
      showcase(body);
      break;
  }

  // CLI-26: hero, picture, three steps, the three messages, a closing call.
  function landingPage(target: HTMLElement): void {
    const how = h("a", {
      href: "#how",
      class: "button",
      onclick: event => {
        event.preventDefault();
        target.querySelector("#how")?.scrollIntoView({ behavior: "smooth" });
      },
    }, hero[lang].how);

    const d = diagram[lang];
    const part = (index: number, name: keyof typeof icons, extra = "") => h("div", { class: `part ${extra}` },
      h("span", { class: "bubble" }, icon(name)),
      h("strong", {}, d.parts[index]!.title),
      h("small", {}, d.parts[index]!.note));
    const link = (kind: string, label: string) => h("div", { class: `link ${kind}` },
      h("span", { class: "line" }, h("i", { class: "dot" })),
      h("small", {}, label));
    const picture = h("figure", { class: "flow" },
      h("div", { class: "flow-row" },
        part(0, "phone"), link("both", d.both), part(1, "gate", "dalan"), link("out", d.outgoing), part(2, "computer")),
      h("figcaption", {}, d.caption));

    const stepList = h("ol", { class: "steps" });
    withNumbers(values => stepList.append(...steps(lang, values).map((step, i) => h("li", { class: "step" },
      h("span", { class: "number" }, language.number(i + 1)),
      h("h3", {}, step.title),
      h("p", {}, step.text),
      ...(step.code ? [codeBlock(step.code)] : [])))));

    const featureIcons: (keyof typeof icons)[] = ["home", "shield", "offline"];
    target.append(
      h("section", { class: "hero" },
        h("div", { class: "column" },
          h("span", { class: "pill" }, hero[lang].badge),
          h("h2", {}, hero[lang].title),
          h("p", { class: "lead" }, hero[lang].text),
          h("div", { class: "row wrap" }, enterButton(), how)),
        picture),
      h("section", { id: "how", class: "band" }, h("h2", {}, hero[lang].how), stepList),
      h("section", { class: "features" }, ...landing[lang].map((message, i) => h("article", {},
        h("span", { class: "bubble" }, icon(featureIcons[i]!)), h("h3", {}, message.title), h("p", {}, message.text)))),
      h("section", { class: "cta" },
        h("h2", {}, closing[lang].title),
        h("p", {}, closing[lang].text),
        h("div", { class: "row wrap" }, enterButton(), pageLink("/showcase", t("showcase"), "button"))));
  }

  /** A code block, always left to right, with a copy button. */
  function codeBlock(code: string): HTMLElement {
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

  const footer = h("footer", { class: "site" },
    h("nav", { class: "links" }, pageLink("/showcase", t("showcase")), pageLink("/faq", t("faq")), pageLink("/privacy", t("privacy"))),
    h("small", {}, `${t("appName")} · ${diagram[lang].parts[1]!.note}`));
  return h("main", { class: "public" }, header, error, h("div", { class: "scroller page" }, body, footer));
}
