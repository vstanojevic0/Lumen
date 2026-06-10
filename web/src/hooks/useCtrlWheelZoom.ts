import { useEffect } from "react";

const MIN_ZOOM = 25;
const MAX_ZOOM = 200;
const STEP = 8;

export function useCtrlWheelZoom(
  onZoomChange: (value: number | ((current: number) => number)) => void,
  enabled = true,
) {
  useEffect(() => {
    if (!enabled) return;

    const onWheel = (event: WheelEvent) => {
      if (!event.ctrlKey && !event.metaKey) return;

      event.preventDefault();
      const delta = event.deltaY > 0 ? -STEP : STEP;
      onZoomChange((current) =>
        Math.min(MAX_ZOOM, Math.max(MIN_ZOOM, current + delta)),
      );
    };

    window.addEventListener("wheel", onWheel, { passive: false });
    return () => window.removeEventListener("wheel", onWheel);
  }, [enabled, onZoomChange]);
}
