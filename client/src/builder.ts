// "My bots" (CLI-24, CLI-49): /bots is the list, /bots/new the create form, /bots/<username> one bot's own page.
import { ApiError, api } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { codeBlock, stepCards, tabbed } from "./codeView.ts";
import { h } from "./dom.ts";
import { botCliCode, botMcpSamples, botRunSteps } from "./guide.ts";
import { botFace, squareIcon } from "./icon.ts";
import { categoryName, showcaseStateName, type PublicNumbers } from "./pages.ts";
import { botByUsername, builderView, TokenHandOver, type BuilderBot, type NewToken } from "./panels.ts";
import { toast } from "./toast.ts";

/** CLI-49: the token from the create form, for the new bot's page only. */
const handOver = new TokenHandOver();

function failedWith(app: App) {
  const t = app.language.t.bind(app.language);
  return (failure: unknown) => {
    toast(failure instanceof ApiError && failure.code === "bad_request" ? t("badBot") : t(errorText(failure)), "error");
  };
}

/** The page title row (docs/UI.md): Back, the title, then the main action. */
function titleRow(back: () => void, backText: string, title: string, ...action: HTMLElement[]): HTMLElement {
  return h("header", {}, h("button", { type: "button", class: "link", onclick: back }, backText), h("h1", {}, title), ...action);
}

/** CLI-23: for a user who is not a builder yet, the next step, right here. */
function builderStep(app: App, redraw: () => void): HTMLElement {
  const t = app.language.t.bind(app.language);
  const view = builderView(app.user);
  const body = view === "phone"
    ? [h("p", { class: "note" }, t("phoneRequired")),
      h("div", { class: "row" }, h("button", { type: "button", class: "primary", onclick: () => app.go("/account/profile") }, t("verifyPhone")))]
    : view === "waiting"
      ? [h("p", { class: "note" }, t("builderWaiting"))]
      : [...(view === "refused" ? [h("p", { class: "note" }, t("builderRefused"))] : []), h("div", { class: "row" }, h("button", {
        type: "button", class: "primary", onclick: async () => {
          try {
            app.user = await api.askBuilder();
            redraw();
          } catch (failure) {
            toast(t(errorText(failure)), "error");
          }
        },
      }, t("askBuilder")))];
  return h("section", { class: "card" }, h("h2", {}, t("building")), h("p", { class: "note" }, t("buildingNote")), ...body);
}

/** CLI-30: the access keys are an account matter; "My bots" and "My apps" only point to them. */
export function keysLink(app: App): HTMLElement {
  return h("p", {}, h("a", {
    href: "/account/keys",
    onclick: event => {
      event.preventDefault();
      app.go("/account/keys");
    },
  }, app.language.t("keysLink")));
}

/** CLI-24: the list of my bots, or the next step for a user who cannot build yet. */
export function myBotsScreen(app: App): Screen {
  const t = app.language.t.bind(app.language);
  const failed = failedWith(app);
  const body = h("div", { class: "scroller" });
  const element = h("main", { class: "wide" });

  function draw(): void {
    const builder = builderView(app.user) === "bots";
    const create = h("button", { type: "button", class: "primary", onclick: () => app.go("/bots/new") }, t("createBot"));
    element.replaceChildren(titleRow(() => app.go("/chats"), t("back"), t("myBots"), ...(builder ? [create] : [])), body);
    if (!builder) {
      body.replaceChildren(builderStep(app, draw));
      return;
    }
    const list = h("ul", { class: "rows bot-list" });
    body.replaceChildren(h("section", { class: "card" }, list), keysLink(app));
    api.builderBots().then(bots => {
      list.replaceChildren(...(bots.length === 0
        ? [h("li", { class: "empty" }, h("p", { class: "note" }, t("noBuilderBots")),
          h("button", { type: "button", class: "primary", onclick: () => app.go("/bots/new") }, t("createBot")))]
        : bots.map(bot => h("li", {}, h("a", {
          class: "line",
          href: `/bots/${bot.username}`,
          onclick: event => {
            event.preventDefault();
            app.go(`/bots/${bot.username}`);
          },
        },
        botFace(bot),
        h("div", { class: "who" }, h("strong", { dir: "auto" }, bot.name), h("span", { class: "note", dir: "ltr" }, `@${bot.username}`)),
        ...(bot.suspended ? [h("span", { class: "badge danger" }, t("suspended"))] : []),
        h("span", { class: "note" }, app.language.seen(bot, new Date())))))));
    }, failed);
  }
  draw();
  return { element };
}

/** CLI-50: a file picker that cuts the picture to a square and gives the icon, with a preview. */
function iconPicker(app: App, onIcon: (icon: Blob) => void): { input: HTMLInputElement; preview: HTMLElement } {
  const t = app.language.t.bind(app.language);
  const preview = h("span", { class: "icon-preview" });
  const input = h("input", { type: "file", accept: "image/png,image/jpeg,image/webp", "aria-label": t("iconPick") });
  input.addEventListener("change", async () => {
    const file = input.files?.[0];
    input.value = "";
    if (!file) return;
    const icon = await squareIcon(file, app.maxBotIconKb);
    if (!icon) {
      toast(t("badIcon"), "error");
      return;
    }
    preview.replaceChildren(h("img", { class: "avatar", src: URL.createObjectURL(icon), alt: "" }));
    onIcon(icon);
  });
  return { input, preview };
}

/** CLI-24: the create form, then the new bot's page with its token. */
export function newBotScreen(app: App): Screen {
  const t = app.language.t.bind(app.language);
  const failed = failedWith(app);
  if (builderView(app.user) !== "bots") return myBotsScreen(app);

  let icon: Blob | null = null;
  const picker = iconPicker(app, picked => { icon = picked; });
  const username = h("input", { id: "new-bot", type: "text", dir: "ltr", autocapitalize: "none", autocomplete: "off", placeholder: "weather_bot", required: true });
  const name = h("input", { id: "new-bot-name", type: "text", dir: "auto", autocomplete: "off", required: true });
  const submit = h("button", { type: "submit", class: "primary" }, t("createBot"));
  const form = h("form", {
    class: "card create",
    onsubmit: async event => {
      event.preventDefault();
      submit.disabled = true;
      try {
        const made = await api.createBot(username.value.trim().replace(/^@/, ""), name.value.trim());
        // The bot exists now: a failed icon is only a toast, and can be set again on its page.
        if (icon) await api.setBotIcon(made.id, icon).catch(failed);
        handOver.give(made);
        toast(t("botMade"));
        app.go(`/bots/${made.username}`);
      } catch (failure) {
        failed(failure);
      } finally {
        submit.disabled = false;
      }
    },
  },
  h("div", { class: "fields" },
    h("div", { class: "field" }, h("label", { for: "new-bot" }, t("botUsername")), username, h("p", { class: "note" }, t("usernameNote"))),
    h("div", { class: "field" }, h("label", { for: "new-bot-name" }, t("name")), name)),
  h("div", { class: "field" }, h("label", {}, t("botIcon")), h("div", { class: "row icon-row" }, picker.preview, picker.input),
    h("p", { class: "note" }, t("botIconNote"))),
  h("div", { class: "row" }, submit));

  const element = h("main", { class: "wide" },
    titleRow(() => app.go("/bots"), t("back"), t("createBot")),
    h("div", { class: "scroller" }, form));
  queueMicrotask(() => username.focus());
  return { element };
}

/** CLI-49: one bot's own page. Every value on it is this bot's. */
export function botPageScreen(app: App, username: string): Screen {
  const t = app.language.t.bind(app.language);
  const failed = failedWith(app);
  const body = h("div", { class: "scroller" });
  const title = h("h1", { dir: "auto" }, `@${username}`);
  const element = h("main", { class: "wide" },
    h("header", {}, h("button", { type: "button", class: "link", onclick: () => app.go("/bots") }, t("back")), title), body);

  let bot: BuilderBot | null = null;
  let token: NewToken | null = null;
  let settings: PublicNumbers | null = null;
  let categories: string[] = [];

  Promise.all([api.builderBots(), api.publicSettings(), api.showcase("")]).then(([bots, numbers, showcase]) => {
    bot = botByUsername(bots, username);
    token = bot ? handOver.take(bot.id) : null;
    settings = numbers;
    categories = showcase.categories;
    draw();
  }, failure => {
    failed(failure);
    body.replaceChildren(h("p", { class: "error" }, t(errorText(failure))));
  });

  function draw(): void {
    if (!bot || !settings) {
      title.textContent = `@${username}`;
      body.replaceChildren(h("section", { class: "card" }, h("p", {}, t("botMissing")),
        h("div", { class: "row" }, h("button", { type: "button", class: "primary", onclick: () => app.go("/bots") }, t("backToBots")))));
      return;
    }
    title.textContent = bot.name;
    body.replaceChildren(headCard(bot), linkCard(bot, settings), ...(token ? [tokenCard(token)] : []),
      showcaseCard(bot), runCard(bot, settings));
  }

  function headCard(current: BuilderBot): HTMLElement {
    const picker = iconPicker(app, async icon => {
      try {
        await api.setBotIcon(current.id, icon);
        current.icon_version += 1;
        toast(t("iconSaved"));
        draw();
      } catch (failure) {
        failed(failure);
      }
    });
    const nameLine = h("div", { class: "who" },
      h("strong", { dir: "auto" }, current.name),
      h("span", { class: "note", dir: "ltr" }, `@${current.username}`),
      h("span", { class: "note" }, app.language.seen(current, new Date())));
    return h("section", { class: "card bot-head" },
      h("div", { class: "line" }, botFace(current), nameLine,
        ...(current.suspended ? [h("span", { class: "badge danger" }, t("suspended"))] : [])),
      h("div", { class: "row wrap" },
        h("button", { type: "button", class: "small", onclick: event => rename(current, event.currentTarget as HTMLElement) }, t("rename")),
        h("label", { class: "button small file" }, t("iconPick"), picker.input),
        ...(current.icon_version > 0 ? [h("button", { type: "button", class: "small", onclick: () => void removeIcon(current) }, t("iconRemove"))] : []),
        h("button", { type: "button", class: "small", onclick: () => void rotate(current) }, t("newToken")),
        h("button", { type: "button", class: "small danger", onclick: () => void remove(current) }, t("delete"))),
      h("p", { class: "note" }, t("botIconNote")));
  }

  function linkCard(current: BuilderBot, numbers: PublicNumbers): HTMLElement {
    const link = `${numbers.site_url}/@${current.username}`;
    return h("section", { class: "card" },
      h("h2", {}, t("botLink")),
      h("div", { class: "row wrap" },
        h("code", { dir: "ltr", class: "link-text" }, link),
        h("button", { type: "button", class: "small", onclick: () => app.go(`/@${current.username}`) }, t("open")),
        h("button", {
          type: "button", class: "small", onclick: async () => {
            await navigator.clipboard.writeText(link);
            toast(t("copied"));
          },
        }, t("copy"))));
  }

  // CLI-24: shown only right after create or New token, and gone on Close or when leaving the page.
  function tokenCard(shown: NewToken): HTMLElement {
    return h("section", { class: "card token" },
      h("p", {}, t("tokenOnce")),
      codeBlock(shown.token, t),
      h("p", {}, t("guideNext")),
      h("div", { class: "row" }, h("button", { type: "button", onclick: () => { token = null; draw(); } }, t("close"))));
  }

  // CLI-29: ask to be listed, change the entry (each change waits for review), or leave.
  function showcaseCard(current: BuilderBot): HTMLElement {
    const entry = current.showcase;
    const changed = (next: BuilderBot["showcase"]) => {
      current.showcase = next;
      draw();
    };
    const run = (work: () => Promise<void>) => { work().then(() => toast(t("saved")), failed); };
    const description = h("textarea", { dir: "auto", "aria-label": t("description"), required: true });
    description.value = entry?.description ?? "";
    const category = h("select", { "aria-label": t("category") },
      ...categories.map(key => h("option", { value: key }, categoryName(app.language.current, key))));
    if (entry) category.value = entry.category;
    return h("details", { class: "card showcase-form" },
      h("summary", {}, `${t("showcase")}: ${showcaseStateName(app.language.current, entry)}`),
      h("form", {
        class: "column",
        onsubmit: event => {
          event.preventDefault();
          run(async () => changed(await api.askShowcase(current.id, description.value.trim(), category.value)));
        },
      },
      h("p", { class: "note" }, t("reviewNote")),
      h("label", {}, t("description")), description,
      h("label", {}, t("category")), category,
      h("button", { type: "submit", class: "primary" }, t("sendForReview")),
      ...(entry ? [h("button", {
        type: "button",
        onclick: () => run(async () => {
          await api.leaveShowcase(current.id);
          changed(null);
        }),
      }, t("leaveShowcase"))] : [])));
  }

  // CLI-38 and CLI-49: the guide for this bot: a Telegram library, the command line, or MCP.
  function runCard(current: BuilderBot, numbers: PublicNumbers): HTMLElement {
    const n = (value: number) => app.language.number(value);
    const guideBot = { id: current.id, username: current.username };
    const panes: [string, () => HTMLElement[]][] = [
      [t("runTelegram"), () => stepCards(botRunSteps(app.language.current, numbers, n, guideBot, token?.token ?? null), 1, t, n)],
      [t("runCli"), () => [h("p", {}, t("cliNote")), codeBlock(botCliCode(numbers.site_url, guideBot), t)]],
      [t("runMcp"), () => [h("p", {}, t("mcpNote")), ...tabbed(botMcpSamples(numbers.site_url, guideBot), t)]],
    ];
    const tabs = h("div", { class: "os-tabs", role: "tablist" });
    const pane = h("div", { class: "steps" });
    const pick = (index: number) => {
      for (const [i, tab] of [...tabs.children].entries()) tab.setAttribute("aria-selected", String(i === index));
      pane.replaceChildren(...panes[index]![1]());
    };
    tabs.append(...panes.map(([label], i) => h("button", { type: "button", role: "tab", onclick: () => pick(i) }, label)));
    pick(0);
    return h("section", { class: "guide" }, h("h2", {}, t("guideRun")), tabs, pane,
      h("p", {}, h("a", { href: "/guide", onclick: event => { event.preventDefault(); app.go("/guide"); } }, t("guideTitle"))));
  }

  // REL-48: the name becomes a small form in its card.
  function rename(current: BuilderBot, button: HTMLElement): void {
    const row = button.closest(".card")!.querySelector(".who")!;
    const input = h("input", { type: "text", dir: "auto", value: current.name, required: true, "aria-label": t("name") });
    row.replaceChildren(h("form", {
      class: "row grow",
      onsubmit: async event => {
        event.preventDefault();
        try {
          const renamed = await api.renameBot(current.id, input.value.trim());
          current.name = renamed.name;
          draw();
          toast(t("saved"));
        } catch (failure) {
          failed(failure);
        }
      },
    }, input, h("button", { type: "submit", class: "primary small" }, t("save")), h("button", { type: "button", class: "small", onclick: draw }, t("close"))));
    input.focus();
  }

  async function removeIcon(current: BuilderBot): Promise<void> {
    try {
      await api.removeBotIcon(current.id);
      current.icon_version = 0;
      toast(t("iconRemoved"));
      draw();
    } catch (failure) {
      failed(failure);
    }
  }

  async function rotate(current: BuilderBot): Promise<void> {
    if (!confirm(t("newTokenConfirm"))) return;
    try {
      token = await api.rotateToken(current.id);
      toast(t("tokenMade"));
      draw();
    } catch (failure) {
      failed(failure);
    }
  }

  // REL-49: gone at once for the owner and its users (the admin keeps its chats), so ask first.
  async function remove(current: BuilderBot): Promise<void> {
    if (!confirm(t("deleteBotConfirm").replace("{name}", `@${current.username}`))) return;
    try {
      await api.deleteBot(current.id);
      toast(t("botDeleted"));
      app.go("/bots");
    } catch (failure) {
      failed(failure);
    }
  }

  return { element };
}
