// "My bots" for builders (CLI-24): the list, a form to create a bot, and the token shown once.
import { ApiError, api } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { h } from "./dom.ts";
import { BuilderList, type NewToken } from "./panels.ts";

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
        h("span", { class: "note" }, app.language.seen({ online: false, last_seen: bot.last_seen }, new Date())))));
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

  api.builderBots().then(bots => {
    state.bots = bots;
    draw();
  }).catch(failed);

  const element = h("main", {},
    h("header", {},
      h("button", { type: "button", class: "link", onclick: () => app.showPanel("account") }, t("back")),
      h("h1", {}, t("builderBots"))),
    h("div", { class: "scroller" }, tokenCard, error, list, create));

  return { element };
}
