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
