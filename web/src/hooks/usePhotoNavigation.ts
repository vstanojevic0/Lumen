import { useCallback, useEffect } from "react";
import type { PhotoItem } from "../types";

function isInteractiveTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT" || tag === "BUTTON") {
    return true;
  }
  return target.isContentEditable;
}

function adjacentPhotoId(
  photos: PhotoItem[],
  currentId: string,
  direction: -1 | 1,
): string | null {
  const index = photos.findIndex((photo) => photo.id === currentId);
  if (index < 0) return null;

  const nextIndex = index + direction;
  if (nextIndex < 0 || nextIndex >= photos.length) return null;
  return photos[nextIndex]?.id ?? null;
}

export function usePhotoNavigation({
  photos,
  selectedId,
  onSelect,
  enabled = true,
}: {
  photos: PhotoItem[];
  selectedId: string;
  onSelect: (id: string) => void;
  enabled?: boolean;
}) {
  const goToPrevious = useCallback(() => {
    if (!enabled || !selectedId) return false;
    const previousId = adjacentPhotoId(photos, selectedId, -1);
    if (!previousId) return false;
    onSelect(previousId);
    return true;
  }, [enabled, photos, selectedId, onSelect]);

  const goToNext = useCallback(() => {
    if (!enabled || !selectedId) return false;
    const nextId = adjacentPhotoId(photos, selectedId, 1);
    if (!nextId) return false;
    onSelect(nextId);
    return true;
  }, [enabled, photos, selectedId, onSelect]);

  useEffect(() => {
    if (!enabled) return;

    const onKeyDown = (event: KeyboardEvent) => {
      if (isInteractiveTarget(event.target)) return;

      if (event.key === "ArrowLeft") {
        if (goToPrevious()) event.preventDefault();
        return;
      }

      if (event.key === "ArrowRight") {
        if (goToNext()) event.preventDefault();
      }
    };

    const onMouseDown = (event: MouseEvent) => {
      if (isInteractiveTarget(event.target)) return;

      if (event.button === 3) {
        if (goToPrevious()) event.preventDefault();
        return;
      }

      if (event.button === 4) {
        if (goToNext()) event.preventDefault();
      }
    };

    const onAuxClick = (event: MouseEvent) => {
      if (isInteractiveTarget(event.target)) return;

      if (event.button === 3) {
        event.preventDefault();
        goToPrevious();
        return;
      }

      if (event.button === 4) {
        event.preventDefault();
        goToNext();
      }
    };

    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("mousedown", onMouseDown);
    window.addEventListener("auxclick", onAuxClick);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("mousedown", onMouseDown);
      window.removeEventListener("auxclick", onAuxClick);
    };
  }, [enabled, goToPrevious, goToNext]);

  return { goToPrevious, goToNext };
}
