import type { AspectRatio, CropRect } from "../types";

export const FULL_CROP: CropRect = { x: 0, y: 0, w: 1, h: 1 };

export function aspectRatioValue(ratio: AspectRatio): number | null {
  switch (ratio) {
    case "1:1":
      return 1;
    case "4:3":
      return 4 / 3;
    case "16:9":
      return 16 / 9;
    default:
      return null;
  }
}

export function clampCropRect(rect: CropRect, minSize = 0.08): CropRect {
  const w = Math.max(minSize, Math.min(1, rect.w));
  const h = Math.max(minSize, Math.min(1, rect.h));
  const x = Math.max(0, Math.min(1 - w, rect.x));
  const y = Math.max(0, Math.min(1 - h, rect.y));
  return { x, y, w, h };
}

/** Largest centered crop rect for a locked aspect ratio inside a 1×1 frame. */
export function defaultCropForAspect(aspect: AspectRatio): CropRect {
  const target = aspectRatioValue(aspect);
  if (!target) return { ...FULL_CROP };

  if (target >= 1) {
    const h = 1;
    const w = Math.min(1, target);
    return clampCropRect({ x: (1 - w) / 2, y: 0, w, h });
  }

  const w = 1;
  const h = Math.min(1, 1 / target);
  return clampCropRect({ x: 0, y: (1 - h) / 2, w, h });
}

export function fitCropToAspect(rect: CropRect, aspect: AspectRatio): CropRect {
  const target = aspectRatioValue(aspect);
  if (!target) return clampCropRect(rect);

  const cx = rect.x + rect.w / 2;
  const cy = rect.y + rect.h / 2;
  let w = rect.w;
  let h = rect.h;
  const current = w / h;

  if (current > target) w = h * target;
  else h = w / target;

  w = Math.min(1, w);
  h = Math.min(1, h);

  return clampCropRect({
    x: cx - w / 2,
    y: cy - h / 2,
    w,
    h,
  });
}

export type CropHandle =
  | "move"
  | "nw"
  | "ne"
  | "sw"
  | "se"
  | "n"
  | "s"
  | "e"
  | "w";

export function resizeCropRect(
  rect: CropRect,
  handle: CropHandle,
  dx: number,
  dy: number,
  aspect: AspectRatio,
  minSize = 0.08,
): CropRect {
  if (handle === "move") {
    return clampCropRect({ ...rect, x: rect.x + dx, y: rect.y + dy }, minSize);
  }

  let { x, y, w, h } = rect;
  const target = aspectRatioValue(aspect);

  if (handle.includes("e")) w += dx;
  if (handle.includes("w")) {
    w -= dx;
    x += dx;
  }
  if (handle.includes("s")) h += dy;
  if (handle.includes("n")) {
    h -= dy;
    y += dy;
  }

  if (target) {
    const anchorEast = handle.includes("e");
    const anchorSouth = handle.includes("s");

    if (handle === "e" || handle === "w") {
      h = w / target;
      if (!anchorSouth) y = rect.y + rect.h - h;
    } else if (handle === "n" || handle === "s") {
      w = h * target;
      if (!anchorEast) x = rect.x + rect.w - w;
    } else {
      w = Math.max(w, h * target);
      h = w / target;
      if (handle.includes("w")) x = rect.x + rect.w - w;
      if (handle.includes("n")) y = rect.y + rect.h - h;
    }
  }

  return clampCropRect({ x, y, w, h }, minSize);
}
