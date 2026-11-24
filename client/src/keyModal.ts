// CLI-65: the access key is made in a modal on the page the person is on, and shown once inside the login command.
import { api } from "./api.ts";
import { errorText } from "./app.ts";
import { loginCommand } from "./appGuide.ts";
import { codeBlock } from "./codeView.ts";
import { h } from "./dom.ts";
import type { TextKey } from "./i18n.ts";
import { toast } from "./toast.ts";

/** Makes a key (POST /api/account/keys) and shows `dalan login <key>` once. `made` runs after the key exists. */
export async function openKeyModal(t: (key: TextKey) => string, made: () => void): Promise<void> {
  const body = h("div", { class: "column" }, h("p", { class: "note", role: "status" }, "…"));
  const dialog = h("dialog", { class: "modal" },
    h("h2", {}, t("makeLoginKey")),
    body,
    h("div", { class: "row" }, h("button", { type: "button", onclick: () => dialog.close() }, t("close"))));
  dialog.addEventListener("close", () => dialog.remove());
  document.body.append(dialog);
  dialog.showModal();
  try {
    const key = await api.makeKey(`agent ${new Date().toISOString().slice(0, 10)}`);
    // The agent's server address; a user who cannot read it still gets a working command for the default server.
    const server = await api.mySites().then(mine => mine.tunnel_url).catch(() => "");
    body.replaceChildren(
      h("p", { class: "note" }, t("keyModalNote")),
      codeBlock(server ? loginCommand(key.key, server) : `dalan login ${key.key}`, t));
    toast(t("loginKeyMade"));
    made();
  } catch (failure) {
    dialog.close();
    toast(t(errorText(failure)), "error");
  }
}
