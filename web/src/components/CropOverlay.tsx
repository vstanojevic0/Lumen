import { useCallback, useEffect, useRef, useState, type CSSProperties } from "react";
import { resizeCropRect, type CropHandle } from "../lib/crop";
import type { AspectRatio, CropRect } from "../types";

interface CropOverlayProps {
  cropRect: CropRect;
  aspectRatio: AspectRatio;
  onChange: (rect: CropRect) => void;
}

export function CropOverlay({ cropRect, aspectRatio, onChange }: CropOverlayProps) {
  const rootRef = useRef<HTMLDivElement>(null);
  const dragRef = useRef<{
    handle: CropHandle;
    startX: number;
    startY: number;
    startRect: CropRect;
  } | null>(null);

  const [size, setSize] = useState({ w: 0, h: 0 });

  useEffect(() => {
    const el = rootRef.current;
    if (!el) return;

    const update = () => {
      const rect = el.getBoundingClientRect();
      setSize({ w: rect.width, h: rect.height });
    };

    update();
    const observer = new ResizeObserver(update);
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  const toNorm = useCallback((clientX: number, clientY: number) => {
    const el = rootRef.current;
    if (!el || size.w <= 0 || size.h <= 0) return { dx: 0, dy: 0 };
    const rect = el.getBoundingClientRect();
    return {
      dx: (clientX - rect.left) / size.w,
      dy: (clientY - rect.top) / size.h,
    };
  }, [size]);

  useEffect(() => {
    const onMove = (event: PointerEvent) => {
      const drag = dragRef.current;
      if (!drag) return;

      const { dx: px, dy: py } = toNorm(event.clientX, event.clientY);
      const { dx: sx, dy: sy } = toNorm(drag.startX, drag.startY);
      const dx = px - sx;
      const dy = py - sy;

      onChange(resizeCropRect(drag.startRect, drag.handle, dx, dy, aspectRatio));
    };

    const onUp = () => {
      dragRef.current = null;
    };

    window.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
    return () => {
      window.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp);
    };
  }, [aspectRatio, onChange, toNorm]);

  const beginDrag = (handle: CropHandle) => (event: React.PointerEvent) => {
    event.preventDefault();
    event.stopPropagation();
    dragRef.current = {
      handle,
      startX: event.clientX,
      startY: event.clientY,
      startRect: cropRect,
    };
  };

  if (size.w <= 0 || size.h <= 0) {
    return <div ref={rootRef} className="absolute inset-0" />;
  }

  const left = cropRect.x * 100;
  const top = cropRect.y * 100;
  const width = cropRect.w * 100;
  const height = cropRect.h * 100;

  const handles: { id: CropHandle; className: string }[] = [
    { id: "nw", className: "left-0 top-0 -translate-x-1/2 -translate-y-1/2 cursor-nwse-resize" },
    { id: "ne", className: "right-0 top-0 translate-x-1/2 -translate-y-1/2 cursor-nesw-resize" },
    { id: "sw", className: "bottom-0 left-0 -translate-x-1/2 translate-y-1/2 cursor-nesw-resize" },
    { id: "se", className: "bottom-0 right-0 translate-x-1/2 translate-y-1/2 cursor-nwse-resize" },
    { id: "n", className: "left-1/2 top-0 -translate-x-1/2 -translate-y-1/2 cursor-ns-resize" },
    { id: "s", className: "bottom-0 left-1/2 -translate-x-1/2 translate-y-1/2 cursor-ns-resize" },
    { id: "w", className: "left-0 top-1/2 -translate-x-1/2 -translate-y-1/2 cursor-ew-resize" },
    { id: "e", className: "right-0 top-1/2 translate-x-1/2 -translate-y-1/2 cursor-ew-resize" },
  ];

  return (
    <div ref={rootRef} className="absolute inset-0 z-20 touch-none">
      <div
        className="absolute border-2 border-[#75c9a3]"
        style={{
          left: `${left}%`,
          top: `${top}%`,
          width: `${width}%`,
          height: `${height}%`,
          boxShadow: "0 0 0 9999px rgba(0, 0, 0, 0.52)",
        }}
        onPointerDown={beginDrag("move")}
      >
        <div className="pointer-events-none absolute inset-0 grid grid-cols-3 grid-rows-3">
          {Array.from({ length: 9 }).map((_, i) => (
            <div key={i} className="border border-white/22" />
          ))}
        </div>

        {handles.map((handle) => (
          <div
            key={handle.id}
            className={`absolute h-3.5 w-3.5 rounded-full border-2 border-[#75c9a3] bg-white shadow-md ${handle.className}`}
            onPointerDown={beginDrag(handle.id)}
          />
        ))}
      </div>
    </div>
  );
}

export function applyCropClipStyle(cropRect: CropRect, active: boolean): CSSProperties {
  if (!active) return {};
  const top = cropRect.y * 100;
  const right = (1 - cropRect.x - cropRect.w) * 100;
  const bottom = (1 - cropRect.y - cropRect.h) * 100;
  const left = cropRect.x * 100;
  return {
    clipPath: `inset(${top}% ${right}% ${bottom}% ${left}%)`,
  };
}
