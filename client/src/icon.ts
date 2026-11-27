// CLI-50: bot icons. The round face (the icon, or the first letter), and the square cut done on the device before upload.
import { h } from "./dom.ts";
import { initial } from "./pages.ts";

/** The address of a bot's icon (REL-51); the version makes a changed icon a new address. Null when the bot has none. */
export function botIconUrl(botId: number, iconVersion: number | undefined): string | null {
  return iconVersion ? `/api/bots/${botId}/icon?v=${iconVersion}` : null;
}

/** A round face: the bot's icon, or its first letter when it has none or the icon fails to load. */
export function botFace(bot: { id: number; name: string; icon_version?: number }): HTMLElement {
  const letter = () => h("span", { class: "avatar", "aria-hidden": "true" }, initial(bot.name));
  const url = botIconUrl(bot.id, bot.icon_version);
  if (!url) return letter();
  const image: HTMLImageElement = h("img", { class: "avatar", src: url, alt: "", loading: "lazy" });
  image.addEventListener("error", () => image.replaceWith(letter()));
  return image;
}

/** The middle square of a picture. */
export function squareCut(width: number, height: number): { x: number; y: number; size: number } {
  const size = Math.min(width, height);
  return { x: Math.floor((width - size) / 2), y: Math.floor((height - size) / 2), size };
}

/** JPEG qualities tried from best to worst until the picture fits. */
export const qualities = [0.9, 0.8, 0.7, 0.6, 0.5, 0.4, 0.3];

/** The first encoding that fits `maxBytes`, trying `qualities` in order; null when none fits. */
export async function firstThatFits(encode: (quality: number) => Promise<Blob>, maxBytes: number): Promise<Blob | null> {
  for (const quality of qualities) {
    const blob = await encode(quality);
    if (blob.size <= maxBytes) return blob;
  }
  return null;
}

/** Side of the uploaded icon in pixels. */
export const iconSide = 512;

/** CLI-50: the middle square of the picked file, 512 by 512, as a JPEG of at most `maxKb`. Null when it cannot fit or be read. */
export async function squareIcon(file: Blob, maxKb: number): Promise<Blob | null> {
  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file);
  } catch {
    return null;
  }
  const cut = squareCut(bitmap.width, bitmap.height);
  const canvas = h("canvas", { width: String(iconSide), height: String(iconSide) });
  const context = canvas.getContext("2d")!;
  // JPEG has no transparency: a white ground, like Telegram.
  context.fillStyle = "#fff";
  context.fillRect(0, 0, iconSide, iconSide);
  context.drawImage(bitmap, cut.x, cut.y, cut.size, cut.size, 0, 0, iconSide, iconSide);
  bitmap.close();
  return firstThatFits(quality => new Promise<Blob>((resolve, reject) =>
    canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error("no image")), "image/jpeg", quality)), maxKb * 1024);
}

/** CLI-77, CLI-78: the line icons of the settings area and the user menu, on a 24 by 24 grid, drawn in the page (rule 7). */
export const glyphs = {
  profile: ["M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2", "M12 11a4 4 0 1 0 0-8a4 4 0 0 0 0 8z"],
  lock: ["M5 11h14v10H5z", "M8 11V7a4 4 0 0 1 8 0v4"],
  shield: ["M12 3l8 3v6c0 5-3.5 8-8 9c-4.5-1-8-4-8-9V6z"],
  bell: ["M6 8a6 6 0 0 1 12 0c0 7 3 9 3 9H3s3-2 3-9", "M10.3 21a1.9 1.9 0 0 0 3.4 0"],
  sliders: ["M4 21v-7", "M4 10V3", "M12 21v-9", "M12 8V3", "M20 21v-5", "M20 12V3", "M1 14h6", "M9 8h6", "M17 16h6"],
  wrench: ["M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z"],
  key: ["M7 21a4 4 0 1 1 0-8a4 4 0 0 1 0 8z", "M10 14L21 3", "M17 7l3 3"],
  bots: ["M5 9h14v10H5z", "M12 9V5", "M12 5h.01", "M9 14h.01", "M15 14h.01"],
  chats: ["M21 12a8 8 0 0 1-11.6 7.1L3 21l1.9-5.4A8 8 0 1 1 21 12z"],
  apps: ["M4 4h6v6H4z", "M14 4h6v6h-6z", "M4 14h6v6H4z", "M14 14h6v6h-6z"],
  settings: ["M12 15a3 3 0 1 0 0-6a3 3 0 0 0 0 6z", "M12 2v3", "M12 19v3", "M2 12h3", "M19 12h3", "M4.9 4.9L7 7", "M17 17l2.1 2.1", "M4.9 19.1L7 17", "M17 7l2.1-2.1"],
  logout: ["M9 21H5V3h4", "M16 17l5-5l-5-5", "M21 12H9"],
  phone: ["M7 2h10v20H7z", "M11 18h2"],
  mail: ["M3 5h18v14H3z", "M3 7l9 6l9-6"],
  github: ["M9 19c-4 1.5-4-2-6-2", "M15 22v-3.9a3.4 3.4 0 0 0-1-2.6c3.3-.4 6.5-1.6 6.5-7.1A5.4 5.4 0 0 0 19 4.8A5 5 0 0 0 18.9 1S17.7.6 15 2.5a13.4 13.4 0 0 0-7 0C5.3.6 4.1 1 4.1 1A5 5 0 0 0 4 4.8a5.4 5.4 0 0 0-1.5 3.7c0 5.5 3.2 6.7 6.5 7.1a3.4 3.4 0 0 0-1 2.6V22"],
  chevron: ["M9 6l6 6l-6 6"],
  // CLI-81: the admin group.
  grid: ["M4 4h7v7H4z", "M13 4h7v7h-7z", "M4 13h7v7H4z", "M13 13h7v7h-7z"],
  inbox: ["M3 13h5l2 3h4l2-3h5", "M5 5h14l2 8v6H3v-6z"],
  users: ["M17 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2", "M9.5 11a4 4 0 1 0 0-8a4 4 0 0 0 0 8z", "M22 21v-2a4 4 0 0 0-3-3.9", "M16 3.1a4 4 0 0 1 0 7.8"],
  star: ["M12 3l2.8 5.7l6.2.9l-4.5 4.4l1.1 6.2L12 17.3l-5.6 2.9l1.1-6.2L3 9.6l6.2-.9z"],
  list: ["M8 6h13", "M8 12h13", "M8 18h13", "M3 6h.01", "M3 12h.01", "M3 18h.01"],
} as const;

export type GlyphName = keyof typeof glyphs;

/** A line icon from `glyphs`, hidden from screen readers (the name beside it says what it is). */
export function glyph(name: GlyphName): SVGSVGElement {
  const ns = "http://www.w3.org/2000/svg";
  const svg = document.createElementNS(ns, "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("aria-hidden", "true");
  svg.classList.add("icon", "glyph");
  for (const d of glyphs[name]) {
    const path = document.createElementNS(ns, "path");
    path.setAttribute("d", d);
    svg.append(path);
  }
  return svg;
}
