// CLI-30: the "AI agents" part of "My bots": access keys (AUTH-21) and the ready prompt.
import { ApiError, api, type AccessKey } from "./api.ts";
import { errorText, type App } from "./app.ts";
import { h } from "./dom.ts";
import { toast } from "./toast.ts";
import { readyPrompt } from "./pages.ts";

export function agentsPart(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  let keys: AccessKey[] = [];
  const list = h("ul", { class: "bots" });
  const shown = h("div", { class: "token column", hidden: true });
  const error = h("p", { class: "error", role: "alert" });
  const prompt = h("textarea", { dir: "auto", readonly: true, "aria-label": t("readyPrompt") });

  const failed = (failure: unknown) => {
    toast(failure instanceof ApiError && failure.code === "key_limit" ? t("keyLimit") : t(errorText(failure)), "error");
  };

  const copyButton = (text: () => string) => {
    return h("div", { class: "row" },
      h("button", {
        type: "button", onclick: async () => {
          await navigator.clipboard.writeText(text());
          toast(t("copied"));
        },
      }, t("copy")));
  };

  function draw(): void {
    list.replaceChildren(...(keys.length === 0 ? [h("li", { class: "note" }, t("noKeys"))] : []),
      ...keys.map(key => h("li", {},
        h("div", { class: "line" },
          h("strong", { dir: "auto" }, key.name),
          h("span", { class: "note" }, t("keyExpires").replace("{n}", app.language.day(key.expires_at))),
          h("button", { type: "button", class: "link", onclick: () => void remove(key.id) }, t("deleteKey"))))));
  }

  // The key is shown once, here, and is never kept in the page's state.
  function show(key: string): void {
    shown.hidden = false;
    shown.replaceChildren(h("p", {}, t("keyOnce")), h("code", { dir: "ltr" }, key), copyButton(() => key),
      h("button", { type: "button", onclick: () => { shown.hidden = true; shown.replaceChildren(); } }, t("close")));
  }

  async function remove(keyId: number): Promise<void> {
    if (!confirm(t("deleteKeyConfirm"))) return;
    error.textContent = "";
    try {
      await api.deleteKey(keyId);
      keys = keys.filter(key => key.id !== keyId);
      draw();
    } catch (failure) {
      failed(failure);
    }
  }

  const name = h("input", { id: "new-key-name", type: "text", dir: "auto", autocomplete: "off", required: true });
  const form = h("form", {
    class: "column",
    onsubmit: async event => {
      event.preventDefault();
      error.textContent = "";
      try {
        const made = await api.makeKey(name.value.trim());
        keys = [...keys, { id: made.id, name: made.name, created_at: made.created_at, expires_at: made.expires_at }];
        name.value = "";
        draw();
        show(made.key);
      } catch (failure) {
        failed(failure);
      }
    },
  },
  h("label", { for: "new-key-name" }, t("keyName")), name,
  h("button", { type: "submit", class: "primary" }, t("makeKey")));

  Promise.all([api.accessKeys(), api.publicSettings()]).then(([found, settings]) => {
    keys = found;
    prompt.value = readyPrompt(app.language.current, settings.site_url, settings.bot_api_url);
    draw();
  }).catch(failed);

  return h("section", { class: "card" },
    h("h2", {}, t("aiAgents")),
    h("p", { class: "note" }, t("aiAgentsNote")),
    error, shown, list, form,
    h("label", {}, t("readyPrompt")), prompt, copyButton(() => prompt.value));
}
