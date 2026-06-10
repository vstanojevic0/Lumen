import { Crop, RotateCcw, RotateCw } from "lucide-react";
import type { ReactNode } from "react";
import type { AspectRatio } from "../types";

const RATIOS: AspectRatio[] = ["free", "1:1", "4:3", "16:9"];

interface PreviewToolBarProps {
  cropMode: boolean;
  aspectRatio: AspectRatio;
  onRotateLeft: () => void;
  onRotateRight: () => void;
  onToggleCrop: () => void;
  onAspectRatio: (ratio: AspectRatio) => void;
}

export function PreviewToolBar({
  cropMode,
  aspectRatio,
  onRotateLeft,
  onRotateRight,
  onToggleCrop,
  onAspectRatio,
}: PreviewToolBarProps) {
  return (
    <div className="pointer-events-none absolute bottom-6 left-1/2 z-30 flex -translate-x-1/2 flex-col items-center gap-2">
      <div className="pointer-events-auto flex items-center gap-1.5 rounded-2xl border border-white/12 bg-[#0f1824]/72 px-2 py-2 shadow-2xl shadow-black/40 backdrop-blur-xl">
        <ToolButton title="Rotate left 90°" onClick={onRotateLeft}>
          <RotateCcw size={18} />
        </ToolButton>
        <ToolButton title="Rotate right 90°" onClick={onRotateRight}>
          <RotateCw size={18} />
        </ToolButton>
        <div className="mx-0.5 h-7 w-px bg-white/12" />
        <ToolButton
          title={cropMode ? "Exit crop" : "Crop"}
          active={cropMode}
          onClick={onToggleCrop}
        >
          <Crop size={18} />
        </ToolButton>
      </div>

      {cropMode ? (
        <div className="pointer-events-auto flex items-center gap-1 rounded-xl border border-white/10 bg-[#0f1824]/78 px-2 py-1.5 shadow-xl shadow-black/35 backdrop-blur-xl">
          {RATIOS.map((ratio) => (
            <button
              key={ratio}
              type="button"
              onClick={() => onAspectRatio(ratio)}
              className={`rounded-lg px-2.5 py-1 text-[11px] font-medium transition ${
                aspectRatio === ratio
                  ? "bg-[#75c9a3]/22 text-[#b8f0d4]"
                  : "text-white/55 hover:bg-white/8 hover:text-white/85"
              }`}
            >
              {ratio === "free" ? "Free" : ratio}
            </button>
          ))}
        </div>
      ) : null}
    </div>
  );
}

function ToolButton({
  children,
  onClick,
  title,
  active = false,
}: {
  children: ReactNode;
  onClick: () => void;
  title: string;
  active?: boolean;
}) {
  return (
    <button
      type="button"
      title={title}
      onClick={onClick}
      className={`flex h-10 w-10 items-center justify-center rounded-xl transition ${
        active
          ? "bg-[#75c9a3]/25 text-[#b8f0d4]"
          : "text-white/75 hover:bg-white/10 hover:text-white"
      }`}
    >
      {children}
    </button>
  );
}
