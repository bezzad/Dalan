// CLI-54: a message's text with its entities (REL-53), drawn with elements and text nodes only, never as HTML.
import type { Entity } from "./messages.ts";

/** A part of a formatted text: plain text, or an entity with its parts inside. */
export type Part = string | { entity: Entity; parts: Part[] };

/**
 * The text as a tree. The server keeps entities apart or one inside the other (REL-53); anything else
 * (an entity past the end, one that crosses another) is dropped here, so the text always shows.
 */
export function formatTree(text: string, entities: readonly Entity[] = []): Part[] {
  const sorted = entities
    .filter(e => e.length > 0 && e.offset >= 0 && e.offset + e.length <= text.length)
    .sort((a, b) => a.offset - b.offset || b.length - a.length);

  /** The parts of text[from, to) with the entities from index `next` that start inside it. */
  function build(from: number, to: number, next: { i: number }): Part[] {
    const parts: Part[] = [];
    let at = from;
    while (next.i < sorted.length) {
      const entity = sorted[next.i]!;
      if (entity.offset >= to) break;
      next.i++;
      const end = entity.offset + entity.length;
      if (entity.offset < at || end > to) continue;
      if (entity.offset > at) parts.push(text.slice(at, entity.offset));
      parts.push({ entity, parts: build(entity.offset, end, next) });
      at = end;
    }
    if (at < to) parts.push(text.slice(at, to));
    return parts;
  }

  return build(0, text.length, { i: 0 });
}

const tags: Record<string, keyof HTMLElementTagNameMap> = {
  bold: "strong", italic: "em", underline: "u", strikethrough: "s", code: "code", pre: "pre",
  spoiler: "span", text_link: "a", blockquote: "blockquote", expandable_blockquote: "blockquote",
};

/** The DOM nodes of the tree. Text goes in as text nodes, so a bot cannot inject markup. */
export function formatNodes(parts: readonly Part[]): Node[] {
  return parts.map(part => {
    if (typeof part === "string") return document.createTextNode(part);
    const { entity } = part;
    const element = document.createElement(tags[entity.type] ?? "span");
    element.append(...formatNodes(part.parts));
    if (entity.type === "text_link" && entity.url && /^https?:\/\//i.test(entity.url)) {
      element.setAttribute("href", entity.url);
      element.setAttribute("target", "_blank");
      element.setAttribute("rel", "noopener noreferrer");
    }
    if (entity.type === "pre" && entity.language) element.setAttribute("data-language", entity.language);
    // A spoiler hides until tapped; an expandable quote opens on a tap.
    if (entity.type === "spoiler" || entity.type === "expandable_blockquote") {
      element.className = entity.type === "spoiler" ? "spoiler" : "expandable";
      element.setAttribute("tabindex", "0");
      element.addEventListener("click", () => element.classList.add("open"));
    }
    return element;
  });
}
