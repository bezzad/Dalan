// CLI-30: the "Access keys" section of the settings area (CLI-77): access keys (AUTH-21), their addresses (AUTH-38) and the ready prompt.
import { ApiError, api, type AccessKey } from "./api.ts";
import { errorText, type App } from "./app.ts";
import { h } from "./dom.ts";
import { toast } from "./toast.ts";
import { readyPrompt } from "./pages.ts";
import { parseAddresses } from "./settings/account.ts";

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
          h("button", { type: "button", class: "link", onclick: () => editAddresses(key) }, t("keyAddresses")),
          h("button", { type: "button", class: "link", onclick: () => void remove(key.id) }, t("deleteKey"))),
        h("span", { class: "note", dir: "auto" }, (key.addresses ?? []).length === 0 ? t("keyAnyAddress") : (key.addresses ?? []).join("، ")),
        ...(editing === key.id ? [addressForm(key)] : []))));
  }

  // AUTH-38: the addresses or ranges a key may be used from, one box; empty is any address.
  let editing: number | null = null;
  function editAddresses(key: AccessKey): void {
    editing = editing === key.id ? null : key.id;
    draw();
  }

  function addressForm(key: AccessKey): HTMLElement {
    const box = h("textarea", { id: `key-addresses-${key.id}`, dir: "ltr", rows: "3", placeholder: "80.244.7.0/24\n2a01::1" });
    box.value = (key.addresses ?? []).join("\n");
    return h("form", {
      class: "column",
      onsubmit: async event => {
        event.preventDefault();
        const addresses = parseAddresses(box.value);
        try {
          await api.setKeyAddresses(key.id, addresses);
          keys = keys.map(each => each.id === key.id ? { ...each, addresses } : each);
          editing = null;
          draw();
          toast(t("saved"));
        } catch (failure) {
          failed(failure);
        }
      },
    },
    h("label", { for: `key-addresses-${key.id}` }, t("keyAddressesHelp")), box,
    h("div", { class: "row" }, h("button", { type: "submit" }, t("save")), h("button", { type: "button", onclick: () => editAddresses(key) }, t("cancel"))));
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
        keys = [...keys, { id: made.id, name: made.name, created_at: made.created_at, expires_at: made.expires_at, addresses: [] }];
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

  // The section head of the settings page (Access keys) already has the title and the note.
  return h("section", { class: "card" },
    error, shown, list, form,
    h("label", {}, t("readyPrompt")), prompt, copyButton(() => prompt.value));
}
