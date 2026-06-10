import type { PhotoOrientation } from "../types";

export function normalizeOrientation(degrees: number): PhotoOrientation {
  const normalized = ((Math.round(degrees) % 360) + 360) % 360;
  if (normalized === 90) return 90;
  if (normalized === 180) return 180;
  if (normalized === 270) return 270;
  return 0;
}

export function rotateOrientation(
  current: PhotoOrientation,
  delta: 90 | -90,
): PhotoOrientation {
  return normalizeOrientation(current + delta);
}

export function totalRotationDegrees(
  orientation: PhotoOrientation,
  straighten: number,
): number {
  return orientation + straighten;
}
