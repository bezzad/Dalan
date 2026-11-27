// CLI-87: the big editor. It moves the chat's own textarea into a modal and back, so the text, the selection, `dir="auto"`
// and the browser's undo are never lost and nothing has to be kept in step. Enter works as in the chat (CLI-85);
// Ctrl+B and Ctrl+I put marks around the selection.
import { coarsePointer, enterAction, insertText, markKey, pressOf } from "./compose.ts";
import { h } from "./dom.ts";
import type { TextKey } from "./i18n.ts";
import { track } from "./overlays.ts";

/** Opens the editor with `box`. `send` sends what is in it; the editor closes first, so the box is back in the chat. */
export function openEditor(t: (key: TextKey) => string, box: HTMLTextAreaElement, send: () => void): void {
  const home = box.parentElement!;
  const next = box.nextSibling;
  const close = h("button", { type: "button" }, t("close"));
  const sendButton = h("button", { type: "button", class: "primary" }, t("send"));
  // Full screen on a phone or a narrow screen; otherwise large in the middle (CLI-87).
  const dialog = h("dialog", { class: `modal editor${coarsePointer() || innerWidth < 600 ? " full" : ""}`, "aria-label": t("message") },
    h("div", { class: "row end" }, close, sendButton));
  dialog.prepend(box);

  const keys = (event: KeyboardEvent) => {
    if (markKey(box, event)) return;
    // Esc closes the editor (the dialog does that itself) and nothing else: the reply bar stays (CLI-52).
    if (event.key === "Escape") { event.stopImmediatePropagation(); return; }
    const action = enterAction(pressOf(event));
    if (action === "none") return;
    event.preventDefault();
    // The chat's own key handler must not send as well.
    event.stopImmediatePropagation();
    if (action === "newline") insertText(box, "\n");
    else { dialog.close(); send(); }
  };
  box.addEventListener("keydown", keys, true);
  // A new screen closes the editor first, so its Send never goes to a chat that is gone.
  const untrack = track(() => dialog.close());
  // Esc closes the dialog by itself; every way out puts the box back where it was, with its text.
  dialog.addEventListener("close", () => {
    untrack();
    box.removeEventListener("keydown", keys, true);
    home.insertBefore(box, next);
    box.style.height = "";
    box.dispatchEvent(new Event("input", { bubbles: true }));
    dialog.remove();
    box.focus();
  });
  close.addEventListener("click", () => dialog.close());
  sendButton.addEventListener("click", () => { dialog.close(); send(); });
  document.body.append(dialog);
  dialog.showModal();
  box.style.height = "";
  box.focus();
}
