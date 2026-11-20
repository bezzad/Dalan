// The Dalan mark (owner, 2026-11-20): an outer arch (like the D of "Dalan") around an inner door (like the a).
// Drawn in the page on a 64 by 64 grid (rule 7). The same shapes are in public/icons/icon.svg, the round favicon.
const outer = "M20 47V29a12 12 0 0 1 24 0v18";
const door = "M27 47V33a5 5 0 0 1 10 0v14z";

/** The gate in the current text colour. `closed`: the door is shut, for the not-found page (CLI-40). */
export function gate(closed = false): SVGSVGElement {
  const ns = "http://www.w3.org/2000/svg";
  const svg = document.createElementNS(ns, "svg");
  svg.setAttribute("viewBox", "0 0 64 64");
  svg.setAttribute("aria-hidden", "true");
  svg.classList.add("gate");
  if (closed) svg.classList.add("closed");
  const arch = document.createElementNS(ns, "path");
  arch.setAttribute("d", outer);
  arch.setAttribute("class", "arch");
  const inner = document.createElementNS(ns, "path");
  inner.setAttribute("d", door);
  inner.setAttribute("class", "door");
  svg.append(arch, inner);
  if (closed) {
    const handle = document.createElementNS(ns, "circle");
    handle.setAttribute("cx", "34");
    handle.setAttribute("cy", "40");
    handle.setAttribute("r", "1.2");
    handle.setAttribute("class", "handle");
    svg.append(handle);
  }
  return svg;
}
