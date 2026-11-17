// "My bots" for builders (CLI-24): the list, a form to create a bot, the token shown once, and AI agents (CLI-30).
import { agentsPart } from "./agents.ts";
import { ApiError, api } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { h } from "./dom.ts";
import { categoryName, showcaseStateName } from "./pages.ts";
import { BuilderList, type BuilderBot, type NewToken } from "./panels.ts";

export function builderScreen(app: App): Screen {
  const t = app.language.t.bind(app.language);
  const state = new BuilderList();
  const list = h("ul", { class: "bots" });
  const tokenCard = h("section", { class: "card token", hidden: true });
  const error = h("p", { class: "error", role: "alert" });

  const failed = (failure: unknown) => {
    error.textContent = failure instanceof ApiError && failure.code === "bad_request" ? t("badBot") : t(errorText(failure));
  };

  function draw(): void {
    list.replaceChildren(...(state.bots.length === 0 ? [h("li", { class: "note" }, t("noBuilderBots"))] : []),
      ...state.bots.map(bot => h("li", {},
        h("div", { class: "line" },
          h("strong", { dir: "auto" }, bot.name),
          h("span", { dir: "ltr" }, `@${bot.username}`),
          ...(bot.suspended ? [h("span", { class: "badge danger" }, t("suspended"))] : []),
          h("button", { type: "button", class: "link", onclick: () => void rotate(bot.id) }, t("newToken"))),
        h("span", { class: "note" }, app.language.seen(bot, new Date())),
        showcasePart(bot))));
    drawToken(state.shownToken);
  }

  // CLI-24: the token is in no list and is gone once closed; the server shows it only in this one answer.
  function drawToken(token: NewToken | null): void {
    tokenCard.hidden = token === null;
    if (!token) return tokenCard.replaceChildren();
    const copied = h("p", { class: "note", role: "status" });
    tokenCard.replaceChildren(
      h("h2", { dir: "ltr" }, `@${token.username}`),
      h("p", {}, t("tokenOnce")),
      h("code", { dir: "ltr" }, token.token),
      h("div", { class: "row" },
        h("button", {
          type: "button", class: "primary", onclick: async () => {
            await navigator.clipboard.writeText(token.token);
            copied.textContent = t("copied");
          },
        }, t("copy")),
        h("button", { type: "button", onclick: () => { state.closeToken(); draw(); } }, t("close"))),
      copied);
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
      error.textContent = "";
      work().catch(failed);
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

  async function rotate(botId: number): Promise<void> {
    if (!confirm(t("newTokenConfirm"))) return;
    error.textContent = "";
    try {
      state.rotated(await api.rotateToken(botId));
      draw();
    } catch (failure) {
      failed(failure);
    }
  }

  const username = h("input", { id: "new-bot", type: "text", dir: "ltr", autocapitalize: "none", autocomplete: "off", placeholder: "weather_bot", required: true });
  const name = h("input", { id: "new-bot-name", type: "text", dir: "auto", autocomplete: "off", required: true });
  const create = h("form", {
    class: "card",
    onsubmit: async event => {
      event.preventDefault();
      error.textContent = "";
      try {
        state.created(await api.createBot(username.value.trim().replace(/^@/, ""), name.value.trim()), new Date());
        username.value = name.value = "";
        draw();
      } catch (failure) {
        failed(failure);
      }
    },
  },
  h("h2", {}, t("createBot")),
  h("label", { for: "new-bot" }, t("botUsername")), username,
  h("p", { class: "note" }, t("usernameNote")),
  h("label", { for: "new-bot-name" }, t("name")), name,
  h("button", { type: "submit", class: "primary" }, t("createBot")));

  Promise.all([api.builderBots(), api.showcase("")]).then(([bots, showcase]) => {
    state.bots = bots;
    categories = showcase.categories;
    draw();
  }).catch(failed);

  const element = h("main", {},
    h("header", {},
      h("button", { type: "button", class: "link", onclick: () => app.showPanel("account") }, t("back")),
      h("h1", {}, t("builderBots"))),
    h("div", { class: "scroller" }, tokenCard, error, list, create, agentsPart(app)));

  return { element };
}
