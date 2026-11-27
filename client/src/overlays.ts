// The viewers (CLI-89, CLI-90) and the big editor (CLI-87) sit over the page. A new screen, a logout or an ended session
// closes them first, so none stays over the next screen or sends into a chat that is gone.

const open = new Set<() => void>();

/** Keeps `close` until the overlay closes; call the answer then. */
export function track(close: () => void): () => void {
  open.add(close);
  return () => { open.delete(close); };
}

/** Closes every open overlay. */
export function closeOverlays(): void {
  for (const close of [...open]) close();
}
