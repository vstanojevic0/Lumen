import { getMediaBase, mediaPreviewUrl, mediaThumbUrl } from "./mediaUrls";

/** Direct loopback URL — no base64 bridge (low RAM). */
export function useThumbnail(path: string | undefined, enabled = true): string {
  if (!enabled || !path || !getMediaBase()) return "";
  return mediaThumbUrl(path);
}

export function usePreview(path: string | undefined, enabled = true): string {
  if (!enabled || !path || !getMediaBase()) return "";
  return mediaPreviewUrl(path);
}
