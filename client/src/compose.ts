// The message box (CLI-85 to CLI-88): Enter like Telegram, a new line that keeps the undo, the growing box,
// the marks of the big editor and a draft per chat. The chat and the big editor both use these, so they cannot differ.

export type EnterAction = "send" | "newline" | "none";

/**
 * CLI-85: what a key press does. A fine pointer (mouse, real keyboard) sends on Enter and adds a line with Shift, Ctrl or Cmd;
 * a coarse pointer (phone, tablet) adds a line, and only the Send button sends. Never while an IME is composing.
 */
export function enterAction(press: { key: string; shift: boolean; ctrl: boolean; meta: boolean; composing: boolean; coarse: boolean }): EnterAction {
  if (press.composing || press.key !== "Enter") return "none";
  if (press.coarse || press.shift || press.ctrl || press.meta) return "newline";
  return "send";
}

/** CLI-85: read at each key press, never once and never from the user agent: a tablet may get a real keyboard while the page is open. */
export function coarsePointer(): boolean {
  return typeof matchMedia === "function" && matchMedia("(pointer: coarse)").matches;
}

/** CLI-85: the press of a keydown event; `keyCode` 229 is how old Safari tells a composing key. */
export function pressOf(event: KeyboardEvent): Parameters<typeof enterAction>[0] {
  return {
    key: event.key, shift: event.shiftKey, ctrl: event.ctrlKey, meta: event.metaKey,
    composing: event.isComposing || event.keyCode === 229, coarse: coarsePointer(),
  };
}

/** Puts text at the cursor through the browser's own editing, so one undo takes it back; `setRangeText` where that is missing. */
export function insertText(box: HTMLTextAreaElement, text: string): void {
  box.focus();
  // execCommand is old but still the only way to add text that the browser's undo knows.
  const done = typeof document.execCommand === "function" && document.execCommand("insertText", false, text);
  if (!done) {
    box.setRangeText(text, box.selectionStart, box.selectionEnd, "end");
    box.dispatchEvent(new Event("input", { bubbles: true }));
  }
}

/** CLI-87: the selection with its marks around it. Dalan does not read them: the bot gets them as plain text. */
export function marked(selected: string, style: "bold" | "italic"): string {
  const mark = style === "bold" ? "**" : "_";
  return `${mark}${selected}${mark}`;
}

/** CLI-87: Ctrl+B or Ctrl+I (Cmd on macOS) in the big editor; true when the key was one of them. */
export function markKey(box: HTMLTextAreaElement, event: KeyboardEvent): boolean {
  if (!(event.ctrlKey || event.metaKey) || event.altKey || event.shiftKey) return false;
  const style = event.key.toLowerCase() === "b" ? "bold" : event.key.toLowerCase() === "i" ? "italic" : null;
  if (!style) return false;
  event.preventDefault();
  const start = box.selectionStart;
  const selected = box.value.slice(start, box.selectionEnd);
  insertText(box, marked(selected, style));
  const mark = style === "bold" ? 2 : 1;
  // The words stay selected, inside their new marks.
  box.setSelectionRange(start + mark, start + mark + selected.length);
  return true;
}

/** CLI-86 and CLI-87: lines from the height, so a long line that wraps counts as more than one. */
export function lineCount(scrollHeight: number, lineHeight: number): number {
  return lineHeight > 0 ? Math.max(1, Math.round(scrollHeight / lineHeight)) : 1;
}

/** CLI-87: the expand button shows for more than 3 lines. */
export function expandShown(lines: number): boolean {
  return lines > 3;
}

/**
 * CLI-86: fits the box to its text; the style sheet stops it at `--compose-max-height`, and past that it scrolls.
 * Returns the number of lines (CLI-87).
 */
export function grow(box: HTMLTextAreaElement): number {
  box.style.height = "auto";
  box.style.height = `${box.scrollHeight}px`;
  const style = getComputedStyle(box);
  const padding = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
  return lineCount(box.scrollHeight - padding, parseFloat(style.lineHeight) || parseFloat(style.fontSize) * 1.4);
}

/** The part of `Storage` the drafts use; a test gives its own. */
export interface DraftStore {
  readonly length: number;
  key(index: number): string | null;
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

const prefix = "dalan.draft.";

/**
 * CLI-88: the unsent text of each chat, only in this browser and only for this user. A private window may refuse storage:
 * then there are no drafts, and nothing else changes.
 */
export class Drafts {
  private readonly store: DraftStore;
  private readonly userId: number;

  constructor(store: DraftStore, userId: number) {
    this.store = store;
    this.userId = userId;
  }

  private key(botId: number): string {
    return `${prefix}${this.userId}.${botId}`;
  }

  load(botId: number): string {
    try {
      return this.store.getItem(this.key(botId)) ?? "";
    } catch {
      return "";
    }
  }

  save(botId: number, text: string): void {
    try {
      if (text === "") this.store.removeItem(this.key(botId));
      else this.store.setItem(this.key(botId), text);
    } catch {
      // No room or no storage: the draft is lost, the chat works.
    }
  }

  remove(botId: number): void {
    this.save(botId, "");
  }

  /** CLI-88: logout. A shared computer must not keep text for the next person. */
  static clearAll(store: DraftStore): void {
    try {
      const keys: string[] = [];
      for (let i = 0; i < store.length; i++) {
        const key = store.key(i);
        if (key?.startsWith(prefix)) keys.push(key);
      }
      for (const key of keys) store.removeItem(key);
    } catch {
      // Nothing to remove.
    }
  }
}
