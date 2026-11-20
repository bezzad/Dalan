// The public pages (CLI-26 to CLI-28, CLI-38): landing, privacy, FAQ, showcase and the guide. They open with or without login.
import { api } from "./api.ts";
import { errorText } from "./app.ts";
import { h } from "./dom.ts";
import { guideSteps, type Sample } from "./guide.ts";
import type { Language } from "./i18n.ts";
import { themeButton, type Theme } from "./theme.ts";
import { categoryName, closing, faqItems, hero, initial, installCommands, labels, landing, notOffered, offers, privacyPoints, problems, products, security, steps, type Product, type PublicNumbers, type ShowcaseBot } from "./pages.ts";

export type PublicPage = "landing" | "privacy" | "faq" | "showcase" | "guide";

export interface PublicContext {
  language: Language;
  theme: Theme;
  loggedIn: boolean;
  /** Goes to an address of the site without a page load. */
  go(path: string): void;
  /** A visitor gets the login screen; a logged-in user goes to the bot list. */
  enter(): void;
  /** The end of the site header: a login button, or the user menu. */
  userArea: HTMLElement;
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
  globe: ["M12 3a9 9 0 1 0 0 18a9 9 0 0 0 0-18z", "M3 12h18", "M12 3c3 3 3 15 0 18", "M12 3c-3 3-3 15 0 18"],
  check: ["M12 3a9 9 0 1 0 0 18a9 9 0 0 0 0-18z", "M8 12l3 3 5-6"],
  terminal: ["M3 5h18v14H3z", "M7 10l3 2-3 2", "M12 15h5"],
};

/** The logo and the name, a link to the home page. Every page has it at the top (owner, 2026-11-20). */
export function siteBrand(name: string, go: (path: string) => void): HTMLElement {
  return h("h1", {}, h("a", {
    href: "/",
    onclick: event => {
      event.preventDefault();
      go("/");
    },
  }, h("span", { class: "mark" }, icon("gate")), name));
}

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
  const lang = language.current;
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
    pageLink("/guide", t("guide")), pageLink("/showcase", t("showcase")), pageLink("/faq", t("faq")), pageLink("/privacy", t("privacy")),
    h("button", { type: "button", class: "link", onclick: context.onLanguage }, t("language")));

  /** Opens the login screen (or the bot list). In the page body a visitor sees "Try it (free)" (owner, 2026-11-18). */
  const enterButton = (label = t("enter")) => h("button", { type: "button", class: "primary", onclick: context.enter }, context.loggedIn ? t("chats") : label);

  // CLI-26: "Problems we solve". A <details> panel; choosing an item closes it.
  const menu = h("details", { class: "menu" },
    h("summary", {}, labels[lang].problems),
    h("div", { class: "panel" }, ...problems[lang].map(group => h("div", { class: "column" },
      h("span", { class: "label" }, group.group),
      ...group.items.map(item => {
        const body = [h("strong", {}, item.title, ...(item.soon ? [h("span", { class: "soon" }, labels[lang].soon)] : [])), h("small", {}, item.text)];
        if (!item.href) return h("div", { class: "item off" }, ...body);
        return h("a", { class: "item", href: item.href, onclick: event => go(event, item.href!) }, ...body);
      })))));

  function go(event: Event, href: string): void {
    menu.open = false;
    if (href.startsWith("#")) {
      event.preventDefault();
      if (page !== "landing") return context.go("/");
      body.querySelector(href)?.scrollIntoView({ behavior: "smooth" });
    } else if (href.startsWith("/") && !href.includes(".")) {
      event.preventDefault();
      context.go(href);
    }
  }

  const header = h("header", { class: "site" },
    siteBrand(t("appName"), context.go),
    menu,
    nav,
    themeButton(context.theme, t),
    context.userArea);

  const body = h("div", { class: "content" });
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
    case "guide":
      body.append(h("h2", { class: "title" }, t("guideTitle")));
      withNumbers(values => body.append(...guideSteps(lang, values, n).map((step, i) => h("section", { class: "card" },
        h("h3", {}, `${n(i + 1)}. ${step.title}`),
        h("p", {}, step.text),
        ...(step.code ? [codeBlock(step.code)] : []),
        ...(step.samples ? tabbed(step.samples) : [])))));
      break;
  }

  // CLI-26: hero, products, try it, steps, the three messages, security, a closing call.
  function landingPage(target: HTMLElement): void {
    const label = (text: string) => h("span", { class: "label" }, text);

    const stepList = h("ol", { class: "steps" });
    const commands = h("div");
    withNumbers(values => {
      stepList.append(...steps(lang, values).map((step, i) => h("li", { class: "step" },
        h("span", { class: "number" }, language.number(i + 1)),
        h("h3", {}, step.title),
        h("p", {}, step.text),
        ...(step.code ? [codeBlock(step.code)] : []))));
      commands.replaceWith(...tabbed(installCommands(values).map(item => ({ name: item.os, code: item.code }))));
    });

    const featureIcons: (keyof typeof icons)[] = ["home", "shield", "offline"];
    const offerIcons: (keyof typeof icons)[] = ["gate", "shield", "terminal", "globe"];
    target.append(
      h("section", { class: "hero glow" },
        h("h2", {}, hero[lang].title, h("br"), h("span", { class: "rainbow" }, hero[lang].glow)),
        h("p", { class: "lead" }, hero[lang].text),
        h("div", { class: "row wrap" }, enterButton(labels[lang].start), h("a", { href: "#how", class: "button", onclick: event => go(event, "#how") }, hero[lang].how))),
      h("section", { class: "products" },
        label(labels[lang].products),
        h("div", { class: "cards" }, ...products[lang].map((product, i) => productCard(product, i === 0 ? "gate" : "globe")))),
      h("section", { id: "offers", class: "offers" },
        label(labels[lang].offers),
        h("h2", {}, labels[lang].offersTitle),
        h("div", { class: "groups" }, ...offers[lang].map((group, i) => h("article", { class: group.items.every(item => item.soon) ? "soon-card" : "" },
          h("span", { class: "badge-icon" }, icon(offerIcons[i]!)),
          h("h3", {}, group.title),
          h("p", {}, group.text),
          h("ul", {}, ...group.items.map(item => h("li", {}, icon("check"), h("span", {}, item.text),
            ...(item.soon ? [h("span", { class: "soon" }, labels[lang].soon)] : []))))))),
        h("p", { class: "note" }, notOffered[lang])),
      h("section", { id: "try", class: "try framed" },
        h("span", { class: "badge-icon" }, icon("terminal")),
        label(labels[lang].try),
        h("h2", {}, labels[lang].tryTitle),
        commands,
        h("p", { class: "note" }, labels[lang].tryNote)),
      h("section", { id: "how", class: "band" }, h("h2", {}, hero[lang].how), stepList, pageLink("/guide", t("guideTitle"), "more")),
      h("section", { id: "features", class: "features" }, ...landing[lang].map((message, i) => h("article", {},
        h("span", { class: "bubble" }, icon(featureIcons[i]!)), h("h3", {}, message.title), h("p", {}, message.text)))),
      h("section", { class: "security" },
        h("h2", {}, labels[lang].security),
        h("ul", {}, ...security[lang].map(item => h("li", {}, icon("check"), item))),
        pageLink("/privacy", t("privacy"), "more")),
      h("section", { class: "cta glow framed" },
        h("h2", {}, closing[lang].title, h("br"), h("span", { class: "rainbow" }, closing[lang].glow)),
        h("p", {}, closing[lang].text),
        h("div", { class: "row wrap" }, enterButton(labels[lang].start), pageLink("/showcase", t("showcase"), "button"))));
  }

  /** A product card with its traffic picture: from, Dalan with its guards, to. */
  function productCard(product: Product, name: keyof typeof icons): HTMLElement {
    return h("article", { class: product.soon ? "product soon-card" : "product" },
      h("span", { class: "badge-icon" }, icon(name)),
      h("span", { class: "label" }, product.label, ...(product.soon ? [h("span", { class: "soon" }, labels[lang].soon)] : [])),
      h("h3", {}, product.title),
      h("p", {}, product.text),
      h("figure", { class: "traffic" },
        h("span", { class: "tag" }, product.from),
        h("span", { class: "wire" }),
        h("div", { class: "hub-row" },
          h("span", { class: "hub" }, icon("gate")),
          h("ul", { class: "guards" }, ...product.guards.map(guard => h("li", {}, icon("check"), guard)))),
        h("span", { class: "wire" }),
        h("div", { class: "split" }, ...product.branches.map(branch => h("div", { class: "branch" },
          h("span", { class: "wire" }),
          h("span", { class: "tag", dir: "ltr" }, branch.via),
          h("span", { class: "wire up" }),
          h("span", { class: "box" }, icon("computer"), h("span", { dir: "ltr" }, branch.to))))),
        h("span", { class: "frame-label" }, product.frame)));
  }

  /** Tabs over code blocks: one tab per name, the first one open. */
  function tabbed(samples: Sample[]): HTMLElement[] {
    const tabs = h("div", { class: "os-tabs", role: "tablist" });
    const command = h("div", { class: "command" });
    const pick = (index: number) => {
      for (const [i, tab] of [...tabs.children].entries()) tab.setAttribute("aria-selected", String(i === index));
      command.replaceChildren(codeBlock(samples[index]!.code));
    };
    tabs.append(...samples.map((sample, i) => h("button", { type: "button", role: "tab", onclick: () => pick(i) }, sample.name)));
    pick(0);
    return [tabs, command];
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

  const column = (title: string, ...items: Node[]) => h("div", { class: "column" }, h("span", { class: "label" }, title), ...items);
  const footer = h("footer", { class: "site" },
    h("div", { class: "columns" },
      column(labels[lang].product, ...products[lang].map(product => h("span", {}, product.label,
        ...(product.soon ? [h("span", { class: "soon" }, labels[lang].soon)] : []))), pageLink("/showcase", t("showcase"))),
      column(labels[lang].problems, ...problems[lang].map(group => h("span", {}, group.group))),
      column(labels[lang].resources, pageLink("/guide", t("guide")), pageLink("/faq", t("faq")), pageLink("/privacy", t("privacy")),
        h("a", { href: "/llms.txt" }, "llms.txt"), h("a", { href: "/dalan.mjs" }, "dalan.mjs"))),
    h("div", { class: "wordmark", "aria-hidden": "true" }, t("appName")));
  return h("main", { class: "public" }, header, error, h("div", { class: "scroller page" }, body, footer));
}
