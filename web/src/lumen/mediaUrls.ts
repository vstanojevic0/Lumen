import type { PhotoItem } from "../types";

export function getMediaBase(): string | null {
  const base = (window as unknown as { __lumenMediaBase?: string }).__lumenMediaBase;
  if (!base) return null;
  return base.endsWith("/") ? base : `${base}/`;
}

export function encodePhotoPath(path: string): string {
  const bytes = new TextEncoder().encode(path);
  let binary = "";
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/g, "");
}

export function mediaThumbUrl(path: string): string {
  const base = getMediaBase();
  if (!base) return "";
  return `${base}media/thumb?p=${encodeURIComponent(encodePhotoPath(path))}`;
}

export function mediaPreviewUrl(path: string): string {
  const base = getMediaBase();
  if (!base) return "";
  return `${base}media/preview?p=${encodeURIComponent(encodePhotoPath(path))}`;
}

/** Keep filmstrip bounded so we do not decode thousands of thumbnails at once. */
export function filmstripWindow(
  photos: PhotoItem[],
  selectedId: string,
  radius = 24,
): PhotoItem[] {
  if (photos.length === 0) return [];
  const index = photos.findIndex((p) => p.id === selectedId);
  if (index < 0) return photos.slice(0, Math.min(40, photos.length));
  const start = Math.max(0, index - radius);
  const end = Math.min(photos.length, index + radius + 1);
  return photos.slice(start, end);
}
