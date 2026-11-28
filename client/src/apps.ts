// CLI-39: "My apps": the builder's sites (SITE-16), kept fresh while the screen is open, and the step-by-step guide:
// install, log in, reserve a name (SITE-24), try it, keep it online as a service (AGT-14).
import { api, type LoginClient, type MySites, type Site } from "./api.ts";
import { errorText, type App, type Screen } from "./app.ts";
import { defaultSiteName, httpCommand, installCommands, loginCommand, serviceCommands } from "./appGuide.ts";
import { keysLink } from "./builder.ts";
import { h } from "./dom.ts";
import { discoveryUrl } from "./loginGuide.ts";
import { builderView } from "./panels.ts";
import { toast } from "./toast.ts";

/** CLI-39: how often the list asks again, so "online" follows the agent. */
const refreshMs = 10_000;

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
      h("div", { class: "row" }, h("button", { type: "button", class: "primary", onclick: () => app.go("/account/builder") }, t("account")))));
    return { element };
  }

  Promise.all([api.mySites(), api.publicSettings(), api.loginClients().catch(() => [] as LoginClient[])])
    .then(([mine, settings, clients]) => body.replaceChildren(...draw(mine, settings.site_url, clients)))
    .catch(failed);
  return { element };

  function draw(mine: MySites, siteUrl: string, clients: LoginClient[]): HTMLElement[] {
    const logins = loginCard(mine, siteUrl, clients);
    const list = h("ul", { class: "rows" });
    // The list follows the agent while this screen is open; it stops when the screen is gone.
    const timer = setInterval(() => {
      if (!list.isConnected) {
        clearInterval(timer);
        return;
      }
      api.mySites().then(fresh => {
        mine.sites = fresh.sites;
        drawList();
      }).catch(() => { /* the next round tries again */ });
    }, refreshMs);
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
        logins.draw();
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
      logins.element,
      keysLink(app),
      guide(mine, siteUrl, site => {
        mine.sites = [...mine.sites.filter(other => other.id !== site.id), site].sort((a, b) => a.name.localeCompare(b.name));
        drawList();
        logins.draw();
      }),
    ];
  }

  function guide(mine: MySites, siteUrl: string, reserved: (site: Site) => void): HTMLElement {
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

    // Step 3: the port and the name; the name can be kept now (SITE-24). Step 4 follows what the user types.
    const port = h("input", { id: "app-port", type: "number", min: "1", max: "65535", value: "3000", dir: "ltr" });
    const name = h("input", { id: "site-name", type: "text", dir: "ltr", autocapitalize: "none", autocomplete: "off", value: defaultSiteName(app.user) });
    const nameNote = h("p", { class: "note" });
    const run = h("div", {});
    const update = () => {
      const site = { port: Number(port.value) || 3000, name: name.value.trim().toLowerCase() || "mysite" };
      nameNote.textContent = t("siteNameNote").replace("{url}", `${mine.scheme}://${site.name}.${mine.domain || "…"}`);
      run.replaceChildren(codeBlock(httpCommand([site])));
    };
    port.addEventListener("input", update);
    name.addEventListener("input", update);
    update();
    const reserve = h("button", {
      type: "button", onclick: async () => {
        const wanted = name.value.trim().toLowerCase();
        try {
          const site = await api.reserveSite(wanted);
          reserved(site);
          toast(t("nameReserved").replace("{name}", site.name));
        } catch (failure) {
          failed(failure);
        }
      },
    }, t("reserveName"));

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
      h("div", { class: "row" }, reserve),
      h("h3", {}, t("step4Title")),
      h("p", { class: "note" }, t("step4Note")),
      run,
      h("p", { class: "note" }, t("manySites")),
      codeBlock(httpCommand([{ port: 3000, name: "mysite" }, { port: 8080, name: "shop" }])),
      h("h3", {}, t("step5Title")),
      h("p", { class: "note" }, t("step5Note")),
      codeBlock(serviceCommands.install),
      h("p", { class: "note" }, t("serviceWindowsNote")),
      h("p", { class: "note" }, t("statusNote")),
      codeBlock(serviceCommands.status));
  }

  /**
   * CLI-97: Login with Dalan for each site: the switch (AUTH-40), the client id, New secret (shown once, with Copy), the discovery
   * address, and "Require login on the proxy" (SITE-26). Drawn again only when the sites change, so a secret on screen stays.
   */
  function loginCard(mine: MySites, siteUrl: string, clients: LoginClient[]): { element: HTMLElement; draw: () => void } {
    const on = new Set(clients.map(client => client.client_id));
    const secrets = new Map<string, string>();
    /** The person pressed Cancel: the switch goes back, with no error. */
    const kept = new Error("kept");
    const list = h("div", {});
    const element = h("section", { class: "card column" },
      h("h2", {}, t("loginWithDalan")),
      h("p", { class: "note" }, t("loginWithDalanNote"), " ", h("a", { href: "/guide/login", onclick: (event: Event) => {
        event.preventDefault();
        app.go("/guide/login");
      } }, t("guideLoginLink"))),
      list);

    function switchRow(id: string, label: string, checked: boolean, change: (value: boolean) => Promise<void>): HTMLElement {
      const box = h("input", { id, type: "checkbox", checked });
      box.addEventListener("change", () => {
        box.disabled = true;
        change(box.checked).catch(failure => {
          box.checked = !box.checked;
          if (failure !== kept) failed(failure);
        }).finally(() => { box.disabled = false; });
      });
      return h("label", { class: "check", for: id }, box, h("span", {}, label));
    }

    function site(entry: Site): HTMLElement {
      const secret = secrets.get(entry.name);
      const newSecret = h("button", { type: "button", class: "small", onclick: async () => {
        if (!confirm(t("newSecretConfirm").replace("{name}", entry.name))) return;
        try {
          const made = await api.newLoginSecret(entry.name);
          secrets.set(entry.name, made.secret ?? "");
          draw();
          toast(t("secretMade"));
        } catch (failure) {
          failed(failure);
        }
      } }, t("newSecret"));
      return h("div", { class: "login-site" },
        h("strong", { dir: "ltr" }, entry.name),
        switchRow(`oidc-${entry.id}`, t("loginOidcSwitch"), on.has(entry.name), async value => {
          if (value) {
            const made = await api.turnOnLogin(entry.name);
            on.add(entry.name);
            if (made.secret) secrets.set(entry.name, made.secret);
            toast(t(made.secret ? "loginOnCopySecret" : "loginOn"));
          } else {
            if (!confirm(t("loginOffConfirm").replace("{name}", entry.name))) throw kept;
            await api.turnOffLogin(entry.name);
            on.delete(entry.name);
            secrets.delete(entry.name);
            toast(t("loginOff"));
          }
          draw();
        }),
        ...(on.has(entry.name) ? [
          h("dl", { class: "facts" },
            h("dt", {}, t("clientId")), h("dd", { dir: "ltr" }, entry.name),
            h("dt", {}, t("discovery")), h("dd", { dir: "ltr" }, discoveryUrl(siteUrl))),
          ...(secret ? [h("p", { class: "note" }, t("secretOnce")), codeBlock(secret)] : []),
          h("div", { class: "row" }, newSecret),
        ] : []),
        switchRow(`proxy-${entry.id}`, t("proxyLoginSwitch"), entry.login === true, async value => {
          const saved = await api.setProxyLogin(entry.id, value);
          entry.login = saved.login;
          toast(t(value ? "proxyLoginOn" : "proxyLoginOff"));
        }),
        h("p", { class: "note" }, t("proxyLoginNote")));
    }

    function draw(): void {
      list.replaceChildren(...(mine.sites.length === 0 ? [h("p", { class: "note" }, t("noSites"))] : mine.sites.map(site)));
    }

    draw();
    return { element, draw };
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
