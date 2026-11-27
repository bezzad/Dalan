// CLI-89: the zoom of the picture viewer. The picture fills its box at scale 1 and is moved with
// `transform: translate(x, y) scale(scale)` from its top-left corner.

export interface View { scale: number; x: number; y: number }
export interface Point { x: number; y: number }
export interface Box { width: number; height: number }

const least = 1;
const most = 4;
const tapScale = 2.5;

/** Scales to `scale` and keeps `point` (in box pixels) where it was. */
export function zoomAt(view: View, scale: number, point: Point): View {
  const next = Math.min(most, Math.max(least, scale));
  const factor = next / view.scale;
  return { scale: next, x: point.x - (point.x - view.x) * factor, y: point.y - (point.y - view.y) * factor };
}

/** Keeps the picture over its whole box: no empty edge shows while zoomed. */
export function clampPan(view: View, box: Box): View {
  const clamp = (value: number, size: number) => Math.min(0, Math.max(size - size * view.scale, value));
  return { scale: view.scale, x: clamp(view.x, box.width) + 0, y: clamp(view.y, box.height) + 0 };
}

/** A double tap or double click: in at that point, or back to full size. */
export function doubleTap(view: View, point: Point, box: Box): View {
  return view.scale > least ? { scale: least, x: 0, y: 0 } : clampPan(zoomAt(view, tapScale, point), box);
}

/** Two fingers: the scale grows with their distance from `startDistance` (the view when they touched) to `distance`. */
export function pinch(startView: View, startDistance: number, distance: number, middle: Point, box: Box): View {
  return clampPan(zoomAt(startView, startView.scale * (distance / Math.max(1, startDistance)), middle), box);
}
