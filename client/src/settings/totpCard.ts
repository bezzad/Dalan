// CLI-92: the second-factor card (AUTH-37) in "Login and security", and the only admin page while an admin has none (SEC-15).
// Set up shows the QR from the server, the secret to copy and a code box; the first right code turns it on and shows the
// recovery codes once, with Copy and Download. An admin has no Turn off (SEC-15).
import { api } from "../api.ts";
import { errorText, type App } from "../app.ts";
import { latinDigits } from "../digits.ts";
import { h } from "../dom.ts";
import { toast } from "../toast.ts";
import { recoveryFile } from "../totp.ts";
import { badge, settingRow } from "./row.ts";

/** The QR comes as SVG text from our own server. It is parsed as XML and added as nodes, never as HTML (dom.ts). */
function svgNode(svg: string): Node {
  const parsed = new DOMParser().parseFromString(svg, "image/svg+xml").documentElement;
  // A parse error comes back as a document, not an <svg>: then nothing is shown.
  return parsed.nodeName.toLowerCase() === "svg" ? document.importNode(parsed, true) : document.createTextNode("");
}

export function totpCard(app: App): HTMLElement {
  const t = app.language.t.bind(app.language);
  const element = h("div", { class: "card totp" });
  const failed = (failure: unknown) => toast(t(errorText(failure)), "error");
  const codeInput = (id: string) => h("input", {
    id, type: "text", inputmode: "numeric", dir: "ltr", autocomplete: "one-time-code", maxlength: "32", required: true });

  function draw(): void {
    const on = app.user.totp === true;
    const start = h("button", { type: "button", onclick: () => void setUp().catch(failed) }, t("setUp"));
    const off = h("button", { type: "button", onclick: () => element.replaceChildren(turnOffForm()) }, t("totpTurnOff"));
    element.replaceChildren(settingRow(t("totpTitle"), t(app.user.admin ? "totpAdminHelp" : "totpHelp"),
      badge(on, t), ...(on ? (app.user.admin ? [] : [off]) : [start])));
  }

  async function setUp(): Promise<void> {
    const started = await api.totpStart();
    const code = codeInput("totp-new");
    const secret = h("code", { dir: "ltr", class: "secret" }, started.secret);
    const form = h("form", {
      class: "column",
      onsubmit: event => {
        event.preventDefault();
        api.totpConfirm(latinDigits(code.value.trim())).then(answer => {
          app.user = { ...app.user, totp: true };
          showCodes(answer.recovery_codes);
        }, failed);
      },
    },
    h("p", { class: "note" }, t("totpScan")),
    h("div", { class: "qr" }, svgNode(started.qr_svg)),
    h("p", { class: "note" }, t("totpOrType")),
    h("div", { class: "row wrap" }, secret,
      h("button", { type: "button", onclick: () => void navigator.clipboard.writeText(started.secret).then(() => toast(t("copied")), failed) }, t("copy"))),
    h("label", { for: "totp-new" }, t("totpCode")), code,
    h("div", { class: "row" }, h("button", { type: "submit" }, t("totpTurnOn")), h("button", { type: "button", onclick: draw }, t("cancel"))));
    element.replaceChildren(form);
    code.focus();
  }

  /** The recovery codes, shown once (AUTH-37). */
  function showCodes(codes: string[]): void {
    const text = recoveryFile(codes, t("recoveryCodesTitle"));
    const file = URL.createObjectURL(new Blob([text], { type: "text/plain" }));
    element.replaceChildren(
      h("h3", {}, t("recoveryCodesTitle")),
      h("p", { class: "note" }, t("recoveryCodesNote")),
      h("ol", { class: "recovery", dir: "ltr" }, ...codes.map(code => h("li", {}, h("code", {}, code)))),
      h("div", { class: "row wrap" },
        h("button", { type: "button", onclick: () => void navigator.clipboard.writeText(codes.join("\n")).then(() => toast(t("copied")), failed) }, t("copy")),
        h("a", { class: "button", href: file, download: "dalan-recovery-codes.txt" }, t("download")),
        h("button", { type: "button", onclick: () => { URL.revokeObjectURL(file); draw(); app.refresh(); } }, t("done"))));
  }

  function turnOffForm(): HTMLElement {
    const code = codeInput("totp-off");
    return h("form", {
      class: "column",
      onsubmit: event => {
        event.preventDefault();
        api.totpOff(latinDigits(code.value.trim())).then(() => {
          app.user = { ...app.user, totp: false };
          toast(t("saved"));
          draw();
        }, failed);
      },
    },
    h("h3", {}, t("totpTurnOff")),
    h("label", { for: "totp-off" }, t("totpCode")), code,
    h("div", { class: "row" }, h("button", { type: "submit" }, t("totpTurnOff")), h("button", { type: "button", onclick: draw }, t("cancel"))));
  }

  draw();
  return element;
}
