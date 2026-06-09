import { useCallback, useRef } from "react";

const MIN_SWIPE_X = 72;
const MAX_SWIPE_Y = 56;

export function useSwipeBack(onBack: () => void, enabled: boolean) {
  const startRef = useRef<{ x: number; y: number } | null>(null);

  const onPointerDown = useCallback(
    (event: React.PointerEvent) => {
      if (!enabled || event.button !== 0) return;
      startRef.current = { x: event.clientX, y: event.clientY };
      event.currentTarget.setPointerCapture(event.pointerId);
    },
    [enabled],
  );

  const onPointerUp = useCallback(
    (event: React.PointerEvent) => {
      if (!enabled || !startRef.current) return;

      const dx = event.clientX - startRef.current.x;
      const dy = event.clientY - startRef.current.y;
      startRef.current = null;

      try {
        event.currentTarget.releasePointerCapture(event.pointerId);
      } catch {
        // ignore
      }

      if (dx >= MIN_SWIPE_X && Math.abs(dy) <= MAX_SWIPE_Y) {
        onBack();
      }
    },
    [enabled, onBack],
  );

  const onPointerCancel = useCallback(() => {
    startRef.current = null;
  }, []);

  return { onPointerDown, onPointerUp, onPointerCancel };
}
