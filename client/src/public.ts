// The public pages (CLI-26 to CLI-28, CLI-38, CLI-56, CLI-57): landing, privacy, FAQ, showcase, the guides, about and contact. They open with or without login.
import { api } from "./api.ts";
import { errorText } from "./app.ts";
import { h } from "./dom.ts";
import { botFace } from "./icon.ts";
import { gate } from "./logo.ts";
import { guideSteps, siteGuideSteps } from "./guide.ts";
import { loginGuideSteps } from "./loginGuide.ts";
import { openKeyModal } from "./keyModal.ts";
import type { BuilderState } from "./panels.ts";
import { stepper } from "./stepper.ts";
import { botDone, siteDone } from "./steps.ts";
import type { Language } from "./i18n.ts";
import { themeButton, type Theme } from "./theme.ts";
import { about, categoryName, closing, faqItems, footerLine, hero, initial, labels, landing, notOffered, offers, privacyPoints, products, security, stripLabel, stripNames, tryNow, type Product, type PublicNumbers, type ShowcaseBot } from "./pages.ts";
import { installCommands } from "./appGuide.ts";
import { toast } from "./toast.ts";

export type PublicPage = "landing" | "privacy" | "faq" | "showcase" | "guide" | "guideBot" | "guideSite" | "guideLogin" | "about" | "contact" | "notFound";

/** Every public page: the same for a visitor and a logged-in user. */
export const publicPages: readonly string[] = ["landing", "privacy", "faq", "showcase", "guide", "guideBot", "guideSite", "guideLogin", "about", "contact", "notFound"];

export interface PublicContext {
  language: Language;
  loggedIn: boolean;
  /** CLI-57: the logged-in user's email, or null for a visitor or a user with none. */
  email: string | null;
  /** CLI-64, CLI-66: the logged-in user for the guides, or null for a visitor. */
  user: GuideUser | null;
  /** Goes to an address of the site without a page load. */
  go(path: string): void;
  /** A visitor gets the login screen; a logged-in user goes to the bot list. */
  enter(): void;
  /** The end of the site header: language, theme, and a login button or the user menu (headerTools). */
  tools: HTMLElement;
}

let numbers: Promise<PublicNumbers> | null = null;

/** CLI-64: what the stepper needs to know about the person. */
export type GuideUser = { name: string; username: string | null; builder: BuilderState; admin: boolean; has_phone: boolean };

/** CLI-64: the site guide's done list from the server. A call that fails counts as "not known", never as an error. */
async function siteFacts(user: GuideUser): Promise<{ done: boolean[]; hasKey: boolean }> {
  const [keys, mine] = await Promise.all([api.accessKeys().catch(() => []), api.mySites().catch(() => null)]);
  const sitesOnline = mine?.sites.filter(site => site.online).length ?? 0;
  return { done: siteDone({ user, keys: keys.length, sitesOnline }), hasKey: keys.length > 0 };
}

/** CLI-64: the bot guide's done list; a visitor has nothing done. */
async function botFacts(user: GuideUser | null): Promise<boolean[]> {
  if (!user) return [];
  const bots = await api.builderBots().catch(() => []);
  return botDone({ user, bots: bots.length });
}

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
  burger: ["M4 7h16", "M4 12h16", "M4 17h16"],
  close: ["M6 6l12 12", "M18 6L6 18"],
  mic: ["M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3z", "M19 10v2a7 7 0 0 1-14 0v-2", "M12 19v3"],
  search: ["M11 4a7 7 0 1 0 0 14a7 7 0 0 0 0-14z", "M16 16l5 5"],
  // CLI-87: two diagonal arrows. CLI-89 and CLI-90: download, open in a new tab.
  expand: ["M14 4h6v6", "M20 4l-7 7", "M10 20H4v-6", "M4 20l7-7"],
  download: ["M12 3v12", "M7 10l5 5 5-5", "M4 20h16"],
  external: ["M14 4h6v6", "M20 4l-9 9", "M18 14v6H4V6h6"],
  // CLI-26: "Try it now" (an arrow out of a box), copy, and the arrow after a link.
  share: ["M14 5l6 6-6 6", "M20 11H11a7 7 0 0 0-7 7v1"],
  copy: ["M9 9h11v11H9z", "M5 15H4V4h11v1"],
  arrow: ["M4 12h16", "M14 6l6 6-6 6"],
};

/** CLI-45: the Dalan mark alone, a link to the home page, at the end of the chat header. */
export function homeMark(name: string, go: (path: string) => void): HTMLElement {
  return h("a", {
    href: "/",
    class: "home",
    "aria-label": name,
    title: name,
    onclick: event => {
      event.preventDefault();
      go("/");
    },
  }, h("span", { class: "mark" }, gate()));
}

/** The logo and the name, a link to the home page. Every page has it at the top (owner, 2026-11-20). */
export function siteBrand(name: string, go: (path: string) => void): HTMLElement {
  return h("h1", {}, h("a", {
    href: "/",
    onclick: event => {
      event.preventDefault();
      go("/");
    },
  }, h("span", { class: "mark" }, gate()), h("span", { class: "name" }, name)));
}

/**
 * The end of every site header (docs/UI.md): language and theme as small round buttons, then "Log in" or the user menu.
 * The language button shows only a globe; the page itself shows the new language (owner, 2026-11-21). Its name says the other language.
 */
export function headerTools(
  language: Language, theme: Theme, onLanguage: () => void, userArea: HTMLElement, bell: HTMLElement | null = null): HTMLElement {
  const t = language.t.bind(language);
  return h("div", { class: "tools" },
    h("button", { type: "button", class: "round lang", title: t("language"), "aria-label": t("language"), onclick: onLanguage },
      icon("globe")),
    themeButton(theme, t),
    // CLI-96: the bell of a signed-in user, before the user menu.
    ...(bell ? [bell] : []),
    userArea);
}

export function icon(name: keyof typeof icons): SVGSVGElement {
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

  /** Opens the login screen (or the bot list). In the page body a visitor sees "Try it (free)" (owner, 2026-11-18). */
  const enterButton = (label: string) => h("button", { type: "button", class: "primary", onclick: context.enter }, context.loggedIn ? t("chats") : label);

  /** A link inside the site, without a page load. "/#plans" goes to the landing page, then to its plans. */
  function go(event: Event, href: string): void {
    event.preventDefault();
    closeMenu();
    const [path = "/", hash] = href.split("#");
    if (!hash) return context.go(path);
    if (page !== "landing") context.go(href);
    document.getElementById(hash)?.scrollIntoView({ behavior: "smooth" });
  }
  const link = (href: string, label: string, kind = "") => h("a", { href, class: kind, onclick: event => go(event, href) }, label);

  // CLI-42: three links only; FAQ and privacy are in the footer. On a phone the links fold into the ☰ button.
  const navLinks = () => [link("/#plans", labels[lang].plans), link("/showcase", t("showcase")), link("/guide", t("guide"))];
  const sheet = h("nav", { class: "sheet", hidden: true, "aria-label": t("menu") }, ...navLinks());
  const burger = h("button", {
    type: "button",
    class: "round burger",
    title: t("menu"),
    "aria-label": t("menu"),
    "aria-expanded": "false",
    onclick: event => {
      // The click must not reach the document, where it would close the menu at once.
      event.stopPropagation();
      if (sheet.hidden) openMenu();
      else closeMenu();
    },
  }, icon("burger"));
  const onKey = (event: Event) => { if ((event as KeyboardEvent).key === "Escape") closeMenu(); };
  const onOutside = (event: Event) => { if (!sheet.contains(event.target as Node)) closeMenu(); };
  function openMenu(): void {
    sheet.hidden = false;
    burger.setAttribute("aria-expanded", "true");
    burger.replaceChildren(icon("close"));
    document.addEventListener("keydown", onKey);
    document.addEventListener("click", onOutside);
  }
  function closeMenu(): void {
    sheet.hidden = true;
    burger.setAttribute("aria-expanded", "false");
    burger.replaceChildren(icon("burger"));
    document.removeEventListener("keydown", onKey);
    document.removeEventListener("click", onOutside);
  }

  const header = h("header", { class: "site" },
    siteBrand(t("appName"), context.go),
    h("nav", { class: "links" }, ...navLinks()),
    context.tools,
    burger,
    sheet);

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
    case "about":
      // CLI-56: only the current language.
      body.append(h("h2", { class: "title" }, t("about")), ...about[lang].map(part =>
        h("section", { class: "card" }, h("h2", {}, part.title), h("p", {}, part.text))));
      break;
    case "contact":
      body.append(h("h2", { class: "title" }, t("contact")), contactForm());
      break;
    case "showcase":
      body.append(h("h2", { class: "title" }, t("showcase")));
      showcase(body);
      break;
    case "notFound":
      body.append(h("section", { class: "not-found" },
        gate(true),
        h("h2", {}, t("notFoundTitle")),
        h("p", {}, t("notFoundText")),
        h("button", { type: "button", class: "primary", onclick: () => context.go("/") }, t("home"))));
      break;
    case "guide":
      // CLI-38: the install steps wait until the person picks a plan.
      body.append(h("h2", { class: "title" }, t("guideChoose")), h("div", { class: "choices" }, ...products[lang].map((product, i) =>
        h("article", {},
          h("span", { class: "badge-icon" }, icon(i === 0 ? "gate" : "globe")),
          h("span", { class: "label" }, product.label),
          h("h3", {}, product.title),
          h("p", {}, product.text),
          link(product.guide, labels[lang].getStarted, "button primary")))),
        // CLI-97: a site's visitors can log in with Dalan.
        h("p", { class: "note" }, t("guideLoginAsk"), " ", link("/guide/login", t("guideLoginLink"))));
      break;
    case "guideBot":
      body.append(h("h2", { class: "title" }, t("guideTitle")));
      // CLI-64: a logged-in person sees what is already done; a visitor sees the same steps, the first one open.
      withNumbers(values => void botFacts(context.user).then(done =>
        body.append(stepper({ steps: guideSteps(lang, values, n), done, t, n }))));
      break;
    case "guideLogin":
      // CLI-97: both ways of Login with Dalan, for everyone; the server knows none of these steps, so each is done by Next.
      body.append(h("h2", { class: "title" }, t("guideLoginTitle")), h("p", { class: "note" }, t("guideLoginNote")));
      withNumbers(values => body.append(stepper({ steps: loginGuideSteps(lang, values), done: [], t, n })));
      break;
    case "guideSite":
      body.append(h("h2", { class: "title" }, t("guideSiteTitle")));
      // CLI-66: a visitor logs in first; the steps come after the login, at this same address (CLI-63).
      if (!context.user) {
        body.append(h("section", { class: "card" },
          h("p", {}, t("guideLoginFirst")),
          h("div", { class: "row" }, h("button", { type: "button", class: "primary", onclick: () => context.enter() }, t("login")))));
        break;
      }
      withNumbers(values => void siteFacts(context.user!).then(facts => body.append(stepper({
        steps: siteGuideSteps(lang, values), done: facts.done, t, n, hasKey: facts.hasKey,
        // CLI-65: the key is made in a modal on this page; the step turns green after it.
        makeKey: made => void openKeyModal(t, made),
      }))));
      break;
  }

  // CLI-26: only what Dalan does, its features and its plans. No install steps: they wait in the guide of each plan.
  function landingPage(target: HTMLElement): void {
    const label = (text: string) => h("span", { class: "label" }, text);
    const soon = () => h("span", { class: "soon" }, labels[lang].soon);
    /** A list with a tick before each item; the tick sits on the first line of the text (docs/UI.md). */
    const ticks = (items: Node[][]) => h("ul", { class: "ticks" }, ...items.map(item => h("li", {}, icon("check"), ...item)));
    const featureIcons: (keyof typeof icons)[] = ["home", "shield", "offline"];
    const offerIcons: (keyof typeof icons)[] = ["gate", "shield", "terminal", "globe"];
    // CLI-104: a colour per card; the plans are green and blue, the offers green, amber, violet and blue.
    const planTones = ["green", "blue"];
    const offerTones = ["green", "amber", "violet", "blue"];
    /** CLI-103: a big section title in the display font. */
    const title = (text: string) => h("h2", { class: "landing-title" }, text);
    /** CLI-105: the names twice in a row, so the loop has no seam; the second copy is for the eye only. */
    const names = (hidden: boolean) => h("ul", hidden ? { "aria-hidden": "true" } : {}, ...stripNames.map(name => h("li", {}, name)));
    const tryPart = h("section", { id: "try", class: "try-now tone-violet" },
      h("span", { class: "badge-icon" }, icon("share")),
      label(tryNow[lang].label),
      h("h2", { class: "landing-title" }, tryNow[lang].title, h("br"), h("em", {}, tryNow[lang].glow)),
      h("p", {}, tryNow[lang].text));
    target.append(
      h("section", { class: "hero" },
        h("h2", {}, hero[lang].title, h("br"), h("span", { class: "accent" }, hero[lang].glow)),
        h("p", { class: "lead" }, hero[lang].text),
        h("div", { class: "row" }, enterButton(labels[lang].start))),
      h("section", { class: "strip", "aria-label": stripLabel[lang] },
        h("p", {}, stripLabel[lang]),
        h("div", { class: "rail", dir: "ltr" }, h("div", { class: "track" }, names(false), names(true)))),
      h("section", { id: "plans", class: "products" },
        label(labels[lang].plans),
        title(labels[lang].plansTitle),
        h("div", { class: "cards" }, ...products[lang].map((product, i) => productCard(product, i === 0 ? "gate" : "globe", planTones[i]!)))),
      tryPart,
      h("section", { id: "offers", class: "offers" },
        label(labels[lang].offers),
        title(labels[lang].offersTitle),
        h("div", { class: "groups" }, ...offers[lang].map((group, i) => h("article", { class: `tone-${offerTones[i]}` },
          h("span", { class: "badge-icon" }, icon(offerIcons[i]!)),
          h("h3", {}, group.title),
          h("p", {}, group.text),
          ticks(group.items.map(item => [h("span", {}, item.text), ...(item.soon ? [soon()] : [])]))))),
        h("p", { class: "note" }, notOffered[lang])),
      h("section", { id: "features", class: "features" },
        label(labels[lang].why),
        h("div", { class: "cards" }, ...landing[lang].map((message, i) => h("article", {},
          h("span", { class: "bubble" }, icon(featureIcons[i]!)), h("h3", {}, message.title), h("p", {}, message.text))))),
      h("section", { class: "security" },
        title(labels[lang].security),
        ticks(security[lang].map(item => [h("span", {}, item)]))),
      h("section", { class: "cta" },
        h("h2", {}, closing[lang].title, h("br"), h("span", { class: "accent" }, closing[lang].glow)),
        h("p", {}, closing[lang].text),
        h("div", { class: "row wrap" }, enterButton(labels[lang].start), link("/showcase", t("showcase"), "button"))));
    // The install line needs the site address from the public settings; the links come right away.
    const links = h("div", { class: "row wrap more" },
      link("/guide/site", tryNow[lang].site, "arrow"), link("/guide/bot", tryNow[lang].bot, "arrow"));
    for (const a of links.children) a.append(icon("arrow"));
    tryPart.append(links);
    (numbers ??= api.publicSettings()).then(values => links.before(installLine(values.site_url), terminal(values)), () => numbers = null);
  }

  /** CLI-26: a small terminal drawn in the page (no video, rule 7): the run command types itself, then the agent's lines. */
  function terminal(values: PublicNumbers): HTMLElement {
    const site = new URL(values.site_url);
    const domain = values.sites_domain || site.host;
    // Built from parts: the built client names no host of its own (CLI-03).
    const local = "http" + "://localhost:3000";
    const lines = [
      `Connecting to ${values.site_url} ...`,
      `Online: ${site.protocol}//mysite.${domain}  ->  ${local}`,
      "Press Ctrl+C to stop.",
    ];
    return h("figure", { class: "terminal", dir: "ltr", "aria-label": "dalan http 3000 --name mysite" },
      h("div", { class: "bar", "aria-hidden": "true" }, h("span"), h("span"), h("span")),
      h("pre", {},
        h("span", { class: "typed" }, h("span", { class: "prompt" }, "$ "), "dalan http 3000 --name mysite"), "\n",
        ...lines.map((line, i) => h("span", { class: `out out-${i + 1}` }, line, "\n"))));
  }

  /** CLI-26: one install line in a pill: the system tabs (Linux and macOS, Windows), the colored command and Copy. */
  function installLine(siteUrl: string): HTMLElement {
    const install = installCommands(siteUrl);
    const systems = [{ name: "Linux / macOS", code: install.unix }, { name: "Windows", code: install.windows }];
    const line = h("code", {});
    const tabs = h("div", { class: "systems", role: "tablist" });
    let current = systems[0]!.code;
    const copy = h("button", {
      type: "button", class: "round copy-line", title: t("copy"), "aria-label": t("copy"),
      onclick: async () => {
        await navigator.clipboard.writeText(current);
        toast(t("copied"), "ok");
      },
    }, icon("copy"));
    const pick = (index: number) => {
      current = systems[index]!.code;
      for (const [i, tab] of [...tabs.children].entries()) tab.setAttribute("aria-selected", String(i === index));
      // The first word, the flags, the address and the rest each have their colour, like a terminal.
      line.replaceChildren(h("span", { class: "prompt" }, index === 0 ? "$ " : "> "), ...current.split(" ").flatMap((word, i) => [
        h("span", { class: i === 0 ? "cmd" : word.startsWith("-") ? "flag" : word.startsWith("http") ? "url" : "word" }, word), " "]).slice(0, -1));
    };
    tabs.append(...systems.map((system, i) => h("button", { type: "button", role: "tab", onclick: () => pick(i) }, system.name)));
    pick(0);
    return h("div", { class: "install" }, tabs, h("div", { class: "line", dir: "ltr" }, line, copy));
  }

  /** A plan: its traffic picture (from, Dalan with its guards, to) and "Get started", which opens its guide. */
  function productCard(product: Product, name: keyof typeof icons, tone: string): HTMLElement {
    return h("article", { class: `product tone-${tone}` },
      h("span", { class: "badge-icon" }, icon(name)),
      h("span", { class: "label" }, product.label),
      h("h3", {}, product.title),
      h("p", {}, product.text),
      h("figure", { class: "traffic" },
        h("span", { class: "tag" }, product.from),
        h("span", { class: "wire" }),
        h("div", { class: "hub-row" },
          h("span", { class: "hub" }, icon("gate")),
          h("ul", { class: "guards ticks" }, ...product.guards.map(guard => h("li", {}, icon("check"), h("span", {}, guard))))),
        h("span", { class: "wire" }),
        h("div", { class: "split" }, ...product.branches.map(branch => h("div", { class: "branch" },
          h("span", { class: "wire" }),
          h("span", { class: "tag", dir: "ltr" }, branch.via),
          h("span", { class: "wire up" }),
          h("span", { class: "box" }, icon("computer"), h("span", { dir: "ltr" }, branch.to))))),
        h("span", { class: "frame-label" }, product.frame)),
      link(product.guide, labels[lang].getStarted, "button learn"));
  }

  /** CLI-57: an email to answer to (the user's own, locked, when they have one) and the text. */
  function contactForm(): HTMLElement {
    const own = context.email;
    const email = h("input", {
      id: "contact-email", type: "email", dir: "ltr", autocomplete: "email", required: true, placeholder: "sara@example.com",
      readonly: own !== null, value: own ?? "" });
    const text = h("textarea", { id: "contact-text", dir: "auto", rows: "6", required: true });
    withNumbers(values => text.setAttribute("maxlength", String(values.contact_max_length)));
    const send = h("button", { type: "submit", class: "primary" }, t("send"));
    const done = h("p", { class: "note", role: "status" });
    return h("form", {
      class: "card contact",
      onsubmit: event => {
        event.preventDefault();
        error.textContent = done.textContent = "";
        send.disabled = true;
        api.contact(email.value.trim(), text.value).then(() => {
          text.value = "";
          done.textContent = t("contactSent");
        }, failed).finally(() => { send.disabled = false; });
      },
    },
    h("p", {}, t("contactNote")),
    h("label", { for: "contact-email" }, t("email")), email,
    h("label", { for: "contact-text" }, t("contactText")), text,
    send, done);
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
      // CLI-50: the bot icon, or the first letter.
      botFace(bot),
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

  // CLI-42: FAQ and privacy live here, not in the header. CLI-56: about and contact too.
  const column = (title: string, ...items: Node[]) => h("div", { class: "column" }, h("span", { class: "label" }, title), ...items);
  // CLI-72: the version comes from the public settings (/health is private, OPS-06), once per page load;
  // when they fail the line has no version.
  const copyright = h("p", { class: "copyright" });
  const drawCopyright = (version: string | null | undefined) => {
    const line = footerLine(language.current, version, new Date());
    copyright.replaceChildren(line.text);
    if (line.version) copyright.append(" ", h("span", { dir: "ltr" }, line.version));
  };
  drawCopyright(null);
  (numbers ??= api.publicSettings()).then(settings => drawCopyright(settings.version), () => numbers = null);
  const footer = h("footer", { class: "site" },
    h("div", { class: "columns" },
      column(labels[lang].product, ...products[lang].map(product => link(product.guide, product.label)), link("/showcase", t("showcase"))),
      column(labels[lang].resources, link("/guide", t("guide")), link("/faq", t("faq")), link("/privacy", t("privacy")),
        h("a", { href: "/llms.txt" }, "llms.txt"), h("a", { href: "/dalan.mjs" }, "dalan.mjs")),
      column(t("appName"), link("/about", t("about")), link("/contact", t("contact")))),
    copyright,
    h("div", { class: "wordmark", "aria-hidden": "true" }, t("appName")));
  return h("main", { class: "public" }, header, error, h("div", { class: "scroller page" }, body, footer));
}
