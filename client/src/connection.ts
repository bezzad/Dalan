// CLI-75: the state of the live stream as a quiet ring around the header icon: the bot's icon in a chat, the Dalan mark elsewhere.
// No bar and no toast. A hidden status text tells screen readers.

export interface ConnectionTexts {
  connected: string;
  connecting: string;
}

export class Connection {
  private readonly find: () => HTMLElement | null;
  private readonly texts: () => ConnectionTexts;
  /** null: no stream yet (the login and public pages): no ring, no tooltip. */
  private down: boolean | null = null;
  private target: HTMLElement | null = null;
  /** Hidden from the eye, read by screen readers; `show()` in main.ts puts it back after each screen change. */
  readonly status: HTMLElement;

  constructor(find: () => HTMLElement | null, texts: () => ConnectionTexts) {
    this.find = find;
    this.texts = texts;
    this.status = document.createElement("div");
    this.status.className = "sr-only";
    this.status.setAttribute("role", "status");
  }

  /** The stream is down (or the server cannot be reached). */
  setDown(): void {
    if (this.down === true) return;
    this.down = true;
    this.status.textContent = this.texts().connecting;
    this.apply();
  }

  /** The stream is open. */
  setUp(): void {
    if (this.down === false) return;
    this.down = false;
    this.status.textContent = this.texts().connected;
    this.apply();
  }

  /** No stream any more (the user logged out). */
  reset(): void {
    this.down = null;
    this.status.textContent = "";
    this.apply();
  }

  /** Puts the ring and the tooltip on the icon of the screen now shown. Called after every screen change. */
  apply(): void {
    this.target?.classList.remove("ring");
    this.target?.removeAttribute("title");
    const target = this.target = this.down === null ? null : this.find();
    if (!target) return;
    target.setAttribute("title", this.down ? this.texts().connecting : this.texts().connected);
    if (this.down) target.classList.add("ring");
  }
}
