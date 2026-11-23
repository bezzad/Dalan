// CLI-39: "My apps": the builder's sites (SITE-16) and the step-by-step guide to run one with the Dalan agent.
import { api, type MySites, type Site } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { httpCommand, installCommands, loginCommand } from "./appGuide.ts";
import { h } from "./dom.ts";
import { builderView } from "./panels.ts";
import { toast } from "./toast.ts";

export function appsScreen(app: App): Screen {
  const t = app.language.t.bind(app.language);
  const failed = (failure: unknown) => toast(t(errorText(failure)), "error");
  const body = h("div", { class: "scroller" });

  const element = h("main", { class: "wide" },
    h("header", {},
      h("button", { type: "button", class: "link", onclick: () => app.go("/") }, t("back")),
      h("h1", {}, t("myApps"))),
    body);

  if (builderView(app.user) !== "bots") {
    body.append(h("section", { class: "card" },
      h("p", {}, t("appsNeedBuilder")),
      h("div", { class: "row" }, h("button", { type: "button", class: "primary", onclick: () => app.go("/account") }, t("account")))));
    return { element };
  }

  Promise.all([api.mySites(), api.publicSettings()])
    .then(([mine, settings]) => body.replaceChildren(...draw(mine, settings.site_url)))
    .catch(failed);
  return { element };

  function draw(mine: MySites, siteUrl: string): HTMLElement[] {
    const list = h("ul", { class: "rows" });
    const drawList = () => list.replaceChildren(
      ...(mine.sites.length === 0 ? [h("li", { class: "note" }, t("noSites"))] : []),
      ...mine.sites.map(site => h("li", {},
        h("div", { class: "line" },
          h("div", { class: "who" },
            h("strong", { dir: "ltr" }, site.name),
            h("a", { class: "note", dir: "ltr", href: site.url, target: "_blank", rel: "noopener" }, site.url)),
          h("span", { class: site.online ? "site-state on" : "site-state note" }, t(site.online ? "siteOnline" : "siteOffline")),
          h("button", { type: "button", class: "small danger", onclick: () => void remove(site) }, t("delete"))))));

    async function remove(site: Site): Promise<void> {
      if (!confirm(t("deleteSiteConfirm").replace("{name}", site.name))) return;
      try {
        await api.deleteSite(site.id);
        mine.sites = mine.sites.filter(other => other.id !== site.id);
        drawList();
        toast(t("siteDeleted"));
      } catch (failure) {
        failed(failure);
      }
    }

    drawList();
    return [
      h("p", { class: "note" }, t("appsNote")),
      ...(mine.domain === "" ? [h("p", { class: "note", role: "status" }, t("sitesOff"))] : []),
      h("section", { class: "card" }, list),
      guide(mine, siteUrl),
    ];
  }

  function guide(mine: MySites, siteUrl: string): HTMLElement {
    const install = installCommands(siteUrl);

    // Step 2: the key is made here and shown once, inside the command.
    const login = h("div", { class: "column" });
    const makeKey = h("button", {
      type: "button", class: "primary", onclick: async () => {
        try {
          const made = await api.makeKey(`agent ${new Date().toISOString().slice(0, 10)}`);
          login.replaceChildren(codeBlock(loginCommand(made.key, mine.tunnel_url)));
          toast(t("loginKeyMade"));
        } catch (failure) {
          failed(failure);
        }
      },
    }, t("makeLoginKey"));
    login.append(h("div", { class: "row" }, makeKey));

    // Step 3: the command follows the port and name the user types.
    const port = h("input", { id: "app-port", type: "number", min: "1", max: "65535", value: "3000", dir: "ltr" });
    const name = h("input", { id: "site-name", type: "text", dir: "ltr", autocapitalize: "none", autocomplete: "off", value: "myapp" });
    const nameNote = h("p", { class: "note" });
    const run = h("div", {});
    const update = () => {
      const site = { port: Number(port.value) || 3000, name: name.value.trim().toLowerCase() || "myapp" };
      nameNote.textContent = t("siteNameNote").replace("{url}", `${mine.scheme}://${site.name}.${mine.domain || "…"}`);
      run.replaceChildren(codeBlock(httpCommand([site])));
    };
    port.addEventListener("input", update);
    name.addEventListener("input", update);
    update();

    return h("section", { class: "card app-guide" },
      h("h2", {}, t("appsGuideTitle")),
      h("h3", {}, t("step1Title")),
      h("p", { class: "note" }, t("step1Note")),
      h("label", {}, t("unixLabel")), codeBlock(install.unix),
      h("label", {}, t("windowsLabel")), codeBlock(install.windows),
      h("h3", {}, t("step2Title")),
      h("p", { class: "note" }, t("step2Note")),
      login,
      h("h3", {}, t("step3Title")),
      h("p", { class: "note" }, t("step3Note")),
      h("div", { class: "fields" },
        h("div", { class: "field" }, h("label", { for: "app-port" }, t("appPort")), port),
        h("div", { class: "field" }, h("label", { for: "site-name" }, t("siteName")), name, nameNote)),
      run,
      h("p", { class: "note" }, t("manySites")),
      codeBlock(httpCommand([{ port: 3000, name: "levelwise" }, { port: 8080, name: "shop" }])),
      h("p", { class: "note" }, t("nextTime")),
      codeBlock("dalan start"));
  }

  /** A command: monospace, dark in both themes, with Copy (docs/UI.md). */
  function codeBlock(code: string): HTMLElement {
    return h("div", { class: "code" },
      h("pre", { dir: "ltr" }, h("code", {}, code)),
      h("button", {
        type: "button", class: "copy", onclick: async () => {
          await navigator.clipboard.writeText(code);
          toast(t("copied"));
        },
      }, t("copy")));
  }
}
