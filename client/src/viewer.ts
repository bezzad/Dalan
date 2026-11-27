// CLI-89: the picture viewer (pinch, double tap, pan, Download, Close). CLI-90: the HTML viewer, a sandboxed frame from the
// view host (REL-57, SEC-16), full screen where the Fullscreen API works and a fixed layer inside the safe areas elsewhere (iPhone).
import { h } from "./dom.ts";
import type { TextKey } from "./i18n.ts";
import { icon } from "./public.ts";
import { clampPan, doubleTap, pinch, type Point, type View } from "./zoom.ts";

type T = (key: TextKey) => string;

/** One tool button of a viewer: an icon with its name for screen readers and as a tooltip. */
function tool(label: string, glyph: Parameters<typeof icon>[0], onclick: () => void): HTMLButtonElement {
  return h("button", { type: "button", class: "tool", "aria-label": label, title: label, onclick }, icon(glyph));
}

/** A download link with the same look as the tool buttons; the file address is an attachment (SEC-12). */
function downloadLink(t: T, href: string, name: string): HTMLAnchorElement {
  return h("a", { class: "button tool", href, download: name, "aria-label": t("download"), title: t("download") }, icon("download"));
}

/**
 * The layer every viewer uses: over the whole chat, the page under it does not scroll, Esc closes it and the focus goes back
 * to what opened it. `onClose` runs once.
 */
function layer(className: string, label: string, onClose: () => void): { element: HTMLElement; close: () => void } {
  const opener = document.activeElement as HTMLElement | null;
  const element = h("div", { class: `viewer ${className}`, role: "dialog", "aria-modal": "true", "aria-label": label });
  let open = true;
  const keys = (event: KeyboardEvent) => {
    if (event.key !== "Escape") return;
    event.preventDefault();
    close();
  };
  function close(): void {
    if (!open) return;
    open = false;
    document.removeEventListener("keydown", keys, true);
    document.body.classList.remove("viewing");
    element.remove();
    onClose();
    opener?.focus();
  }
  document.addEventListener("keydown", keys, true);
  document.body.classList.add("viewing");
  document.body.append(element);
  return { element, close };
}

/** CLI-89: the picture full screen. Two fingers pinch, a double tap or double click zooms at that point, one finger pans. */
export function openPicture(t: T, src: string, name: string): () => void {
  const picture = h("img", { src, alt: name, draggable: "false" });
  const stage = h("div", { class: "stage" }, picture);
  const view = layer("picture", name, () => {});
  view.element.append(stage, h("div", { class: "tools" }, downloadLink(t, src, name), tool(t("close"), "close", view.close)));

  let current: View = { scale: 1, x: 0, y: 0 };
  const draw = () => { picture.style.transform = `translate(${current.x}px, ${current.y}px) scale(${current.scale})`; };
  const box = () => ({ width: picture.clientWidth, height: picture.clientHeight });
  const at = (event: PointerEvent): Point => {
    const rect = stage.getBoundingClientRect();
    return { x: event.clientX - rect.left - picture.offsetLeft, y: event.clientY - rect.top - picture.offsetTop };
  };

  const pointers = new Map<number, Point>();
  let pinchStart: { view: View; distance: number } | null = null;
  let panStart: { view: View; point: Point } | null = null;
  let lastTap = -Infinity;
  const distance = () => { const [a, b] = [...pointers.values()]; return Math.hypot(a!.x - b!.x, a!.y - b!.y); };
  const middle = (): Point => { const [a, b] = [...pointers.values()]; return { x: (a!.x + b!.x) / 2, y: (a!.y + b!.y) / 2 }; };

  stage.addEventListener("pointerdown", event => {
    stage.setPointerCapture(event.pointerId);
    pointers.set(event.pointerId, at(event));
    if (pointers.size === 2) { pinchStart = { view: current, distance: distance() }; panStart = null; return; }
    // A double tap or a double click: two presses of one pointer close together in time (mouse, finger and pen alike).
    const now = event.timeStamp;
    if (now - lastTap < 300) {
      current = doubleTap(current, at(event), box());
      lastTap = -Infinity;
      draw();
      return;
    }
    lastTap = now;
    panStart = { view: current, point: at(event) };
  });
  stage.addEventListener("pointermove", event => {
    if (!pointers.has(event.pointerId)) return;
    pointers.set(event.pointerId, at(event));
    if (pinchStart && pointers.size === 2) current = pinch(pinchStart.view, pinchStart.distance, distance(), middle(), box());
    else if (panStart && current.scale > 1) {
      const point = at(event);
      current = clampPan({ ...panStart.view, x: panStart.view.x + point.x - panStart.point.x, y: panStart.view.y + point.y - panStart.point.y }, box());
    } else return;
    draw();
  });
  const up = (event: PointerEvent) => {
    pointers.delete(event.pointerId);
    if (pointers.size < 2) pinchStart = null;
    if (pointers.size === 0) panStart = null;
  };
  stage.addEventListener("pointerup", up);
  stage.addEventListener("pointercancel", up);
  view.element.querySelector<HTMLButtonElement>("button.tool")?.focus();
  return view.close;
}

/** CLI-90: the Fullscreen API, with Safari's old names (iPad). iPhone Safari has none for a page: then the fixed layer. */
function fullscreen(): { enabled: boolean; request(element: HTMLElement): Promise<void>; exit(): void; active(): boolean } {
  const doc = document as Document & { webkitFullscreenEnabled?: boolean; webkitFullscreenElement?: Element | null; webkitExitFullscreen?: () => void };
  const enabled = Boolean(document.fullscreenEnabled || doc.webkitFullscreenEnabled);
  return {
    enabled,
    request: element => {
      const webkit = element as HTMLElement & { webkitRequestFullscreen?: () => void };
      if (element.requestFullscreen) return element.requestFullscreen();
      webkit.webkitRequestFullscreen?.();
      return Promise.resolve();
    },
    exit: () => {
      if (document.fullscreenElement) void document.exitFullscreen().catch(() => {});
      else if (doc.webkitFullscreenElement) doc.webkitExitFullscreen?.();
    },
    active: () => Boolean(document.fullscreenElement || doc.webkitFullscreenElement),
  };
}

/**
 * CLI-90: the HTML file in a frame from the view host. The frame never gets the same-origin right, and the view host's own CSP
 * sandboxes it again (SEC-16): its script runs, but it never reaches this page, its cookies or its storage.
 */
export function openHtml(t: T, url: string, downloadUrl: string, name: string): () => void {
  const screen = fullscreen();
  const frame = h("iframe", {
    src: url, title: name, sandbox: "allow-scripts allow-popups", referrerpolicy: "no-referrer",
  });
  // On load the frame gets the focus, so the page's own keys (← →) reach it.
  frame.addEventListener("load", () => frame.focus());
  const view = layer(screen.enabled ? "html full" : "html", name, () => {
    document.removeEventListener("fullscreenchange", left);
    document.removeEventListener("webkitfullscreenchange", left);
    if (screen.active()) screen.exit();
  });
  const left = () => { if (!screen.active()) view.close(); };
  view.element.append(
    h("div", { class: "tools" },
      h("span", { class: "name", dir: "auto" }, name),
      tool(t("openNewTab"), "external", () => { window.open(url, "_blank", "noopener,noreferrer"); }),
      downloadLink(t, downloadUrl, name),
      tool(t("close"), "close", view.close)),
    frame);
  if (screen.enabled) {
    // Leaving full screen (Esc, the browser's own button) closes the viewer.
    screen.request(view.element).then(() => {
      document.addEventListener("fullscreenchange", left);
      document.addEventListener("webkitfullscreenchange", left);
    }, () => view.element.classList.remove("full"));
  }
  frame.focus();
  return view.close;
}
