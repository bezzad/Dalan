// A tiny helper to build DOM nodes. Text always goes in as text, never as HTML, so a bot cannot inject markup.

type Props = Record<string, string | boolean | ((event: Event) => void)>;

export function h<K extends keyof HTMLElementTagNameMap>(
  tag: K, props: Props = {}, ...children: (Node | string)[]): HTMLElementTagNameMap[K] {
  const element = document.createElement(tag);
  for (const [name, value] of Object.entries(props)) {
    if (typeof value === "function") element.addEventListener(name.slice(2), value);
    else if (value === true) element.setAttribute(name, "");
    else if (value !== false) element.setAttribute(name, value);
  }
  element.append(...children);
  return element;
}
