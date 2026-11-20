// "My bots" for builders (CLI-24): the list, a form to create a bot, the token shown once, and AI agents (CLI-30).
import { agentsPart } from "./agents.ts";
import { ApiError, api } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { h } from "./dom.ts";
import { toast } from "./toast.ts";
import { categoryName, showcaseStateName } from "./pages.ts";
import { BuilderList, type BuilderBot, type NewToken } from "./panels.ts";

export function builderScreen(app: App): Screen {
  const t = app.language.t.bind(app.language);
  const state = new BuilderList();
  const list = h("ul", { class: "rows" });
  const tokenCard = h("section", { class: "card token", hidden: true });

  const failed = (failure: unknown) => {
    toast(failure instanceof ApiError && failure.code === "bad_request" ? t("badBot") : t(errorText(failure)), "error");
  };

  function draw(): void {
    list.replaceChildren(...(state.bots.length === 0 ? [h("li", { class: "note" }, t("noBuilderBots"))] : []),
      ...state.bots.map(bot => h("li", {},
        h("div", { class: "line" },
          h("div", { class: "who" },
            h("strong", { dir: "auto" }, bot.name),
            h("span", { class: "note", dir: "ltr" }, `@${bot.username}`)),
          h("span", { class: "note" }, app.language.seen(bot, new Date())),
          ...(bot.suspended ? [h("span", { class: "badge danger" }, t("suspended"))] : []),
          h("button", { type: "button", class: "small", onclick: event => rename(bot, event.currentTarget as HTMLElement) }, t("rename")),
          h("button", { type: "button", class: "small", onclick: () => void rotate(bot.id) }, t("newToken")),
          h("button", { type: "button", class: "small danger", onclick: () => void remove(bot) }, t("delete"))),
        showcasePart(bot))));
    drawToken(state.shownToken);
  }

  // CLI-24: the token is in no list and is gone once closed; the server shows it only in this one answer.
  function drawToken(token: NewToken | null): void {
    tokenCard.hidden = token === null;
    if (!token) return tokenCard.replaceChildren();
    tokenCard.replaceChildren(
      h("h2", { dir: "ltr" }, `@${token.username}`),
      h("p", {}, t("tokenOnce")),
      h("code", { dir: "ltr" }, token.token),
      h("div", { class: "row" },
        h("button", {
          type: "button", class: "primary", onclick: async () => {
            await navigator.clipboard.writeText(token.token);
            toast(t("copied"));
          },
        }, t("copy")),
        h("button", { type: "button", onclick: () => { state.closeToken(); draw(); } }, t("close"))));
  }

  // CLI-29: ask to be listed, change the entry (each change waits for review), or leave.
  let categories: string[] = [];
  function showcasePart(bot: BuilderBot): HTMLElement {
    const entry = bot.showcase;
    const changed = (next: BuilderBot["showcase"]) => {
      bot.showcase = next;
      draw();
    };
    const run = (work: () => Promise<void>) => {
      work().then(() => toast(t("saved")), failed);
    };
    const description = h("textarea", { dir: "auto", "aria-label": t("description"), required: true });
    description.value = entry?.description ?? "";
    const category = h("select", { "aria-label": t("category") },
      ...categories.map(key => h("option", { value: key }, categoryName(app.language.current, key))));
    if (entry) category.value = entry.category;
    const icon = h("input", { type: "file", accept: "image/png,image/jpeg,image/webp", "aria-label": t("icon") });
    icon.addEventListener("change", () => run(async () => {
      const file = icon.files?.[0];
      if (file) changed(await api.setShowcaseIcon(bot.id, file));
    }));

    return h("details", { class: "showcase-form" },
      h("summary", {}, `${t("showcase")}: ${showcaseStateName(app.language.current, entry)}`),
      h("form", {
        class: "column",
        onsubmit: event => {
          event.preventDefault();
          run(async () => changed(await api.askShowcase(bot.id, description.value.trim(), category.value)));
        },
      },
      h("p", { class: "note" }, t("reviewNote")),
      h("label", {}, t("description")), description,
      h("label", {}, t("category")), category,
      h("button", { type: "submit", class: "primary" }, t("sendForReview")),
      ...(entry ? [
        h("label", {}, t("icon")),
        ...(entry.icon ? [h("img", { class: "avatar", src: `${api.ownIconUrl(bot.id)}?v=${Date.now()}`, alt: "" })] : []),
        icon,
        h("button", {
          type: "button",
          onclick: () => run(async () => {
            await api.leaveShowcase(bot.id);
            changed(null);
          }),
        }, t("leaveShowcase")),
      ] : [])));
  }

  // REL-48: the name becomes a small form in its row.
  function rename(bot: BuilderBot, button: HTMLElement): void {
    const line = button.closest(".line")!;
    const input = h("input", { type: "text", dir: "auto", value: bot.name, required: true, "aria-label": t("name") });
    line.replaceChildren(h("form", {
      class: "row grow",
      onsubmit: async event => {
        event.preventDefault();
        try {
          state.renamed(await api.renameBot(bot.id, input.value.trim()));
          draw();
          toast(t("saved"));
        } catch (failure) {
          failed(failure);
        }
      },
    }, input, h("button", { type: "submit", class: "primary small" }, t("save")), h("button", { type: "button", class: "small", onclick: draw }, t("close"))));
    input.focus();
  }

  // REL-49: gone at once for the owner and its users (the admin keeps its chats), so ask first.
  async function remove(bot: BuilderBot): Promise<void> {
    if (!confirm(t("deleteBotConfirm").replace("{name}", `@${bot.username}`))) return;
    try {
      await api.deleteBot(bot.id);
      state.deleted(bot.id);
      draw();
      toast(t("botDeleted"));
    } catch (failure) {
      failed(failure);
    }
  }

  async function rotate(botId: number): Promise<void> {
    if (!confirm(t("newTokenConfirm"))) return;
    try {
      state.rotated(await api.rotateToken(botId));
      draw();
      toast(t("tokenMade"));
    } catch (failure) {
      failed(failure);
    }
  }

  const username = h("input", { id: "new-bot", type: "text", dir: "ltr", autocapitalize: "none", autocomplete: "off", placeholder: "weather_bot", required: true });
  const name = h("input", { id: "new-bot-name", type: "text", dir: "auto", autocomplete: "off", required: true });
  const create = h("form", {
    class: "card create",
    hidden: true,
    onsubmit: async event => {
      event.preventDefault();
      try {
        state.created(await api.createBot(username.value.trim().replace(/^@/, ""), name.value.trim()), new Date());
        username.value = name.value = "";
        create.hidden = true;
        draw();
        toast(t("botMade"));
      } catch (failure) {
        failed(failure);
      }
    },
  },
  h("h2", {}, t("createBot")),
  h("div", { class: "fields" },
    h("div", { class: "field" }, h("label", { for: "new-bot" }, t("botUsername")), username, h("p", { class: "note" }, t("usernameNote"))),
    h("div", { class: "field" }, h("label", { for: "new-bot-name" }, t("name")), name)),
  h("div", { class: "row" },
    h("button", { type: "submit", class: "primary" }, t("createBot")),
    h("button", { type: "button", onclick: () => { create.hidden = true; } }, t("close"))));

  Promise.all([api.builderBots(), api.showcase("")]).then(([bots, showcase]) => {
    state.bots = bots;
    categories = showcase.categories;
    // With no bot yet, the form is the next step: open it.
    if (bots.length === 0) create.hidden = false;
    draw();
  }).catch(failed);

  const element = h("main", { class: "wide" },
    h("header", {},
      h("button", { type: "button", class: "link", onclick: () => app.showPanel(null) }, t("back")),
      h("h1", {}, t("builderBots")),
      h("button", { type: "button", class: "primary", onclick: () => { create.hidden = false; username.focus(); } }, t("createBot"))),
    h("div", { class: "scroller" }, tokenCard, create, h("section", { class: "card" }, list), agentsPart(app)));

  return { element };
}
