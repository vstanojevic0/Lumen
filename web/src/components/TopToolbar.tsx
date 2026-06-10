import {
  ArrowLeft,
  Download,
  PanelRightClose,
  PanelRightOpen,
  Redo2,
  SlidersHorizontal,
  Star,
  Undo2,
} from "lucide-react";
import type { ReactNode } from "react";
import type { AppMode } from "../types";

interface TopToolbarProps {
  mode: AppMode;
  photoTitle?: string;
  favorite?: boolean;
  onToggleFavorite?: () => void;
  inspectorOpen?: boolean;
  onToggleInspector?: () => void;
  zoom: number;
  onZoomChange: (z: number) => void;
  canUndo: boolean;
  canRedo: boolean;
  onUndo: () => void;
  onRedo: () => void;
  onExport: () => void;
  onBack?: () => void;
}

export function TopToolbar({
  mode,
  photoTitle,
  favorite = false,
  onToggleFavorite,
  inspectorOpen = true,
  onToggleInspector,
  zoom,
  onZoomChange,
  canUndo,
  canRedo,
  onUndo,
  onRedo,
  onExport,
  onBack,
}: TopToolbarProps) {
  return (
    <header className="lumen-toolbar flex h-[60px] shrink-0 items-center gap-3 border-b border-white/8 px-5">
      {mode === "library" ? (
        <>
          <div className="text-sm font-medium text-white/55">Library</div>
          <div className="ml-auto">
            <ZoomControl zoom={zoom} onZoomChange={onZoomChange} />
          </div>
        </>
      ) : (
        <>
          {onBack ? (
            <button
              type="button"
              onClick={onBack}
              className="flex h-10 items-center gap-2 rounded-xl border border-white/7 bg-white/8 px-3 text-sm font-medium text-white/88 transition hover:border-white/14 hover:bg-white/12"
              title="Back to library (Esc)"
            >
              <ArrowLeft size={16} className="text-white/65" />
              <span>Library</span>
            </button>
          ) : null}

          {onToggleInspector ? (
            <button
              type="button"
              onClick={onToggleInspector}
              title={inspectorOpen ? "Hide edit panel" : "Show edit panel"}
              className={`flex h-10 items-center gap-2 rounded-xl border px-3.5 text-sm font-medium transition ${
                inspectorOpen
                  ? "border-[#087bff]/45 bg-[#087bff]/14 text-white"
                  : "border-white/7 bg-white/8 text-white/75 hover:border-white/14 hover:bg-white/12 hover:text-white"
              }`}
            >
              <SlidersHorizontal size={17} />
              <span>Edit panel</span>
              {inspectorOpen ? (
                <PanelRightClose size={16} className="text-white/55" />
              ) : (
                <PanelRightOpen size={16} className="text-white/55" />
              )}
            </button>
          ) : null}

          {photoTitle ? (
            <div
              className="hidden min-w-0 max-w-[min(24vw,280px)] truncate text-sm text-white/45 lg:block"
              title={photoTitle}
            >
              {photoTitle}
            </div>
          ) : null}

          <div className="ml-auto flex items-center gap-2">
            {onToggleFavorite ? (
              <button
                type="button"
                onClick={onToggleFavorite}
                title={favorite ? "Remove from favorites" : "Add to favorites"}
                className={`flex h-10 w-10 items-center justify-center rounded-xl border transition ${
                  favorite
                    ? "border-[#f5c842]/45 bg-[#f5c842]/14 text-[#f5c842]"
                    : "border-white/7 bg-white/8 text-white/55 hover:border-white/14 hover:text-white"
                }`}
              >
                <Star size={17} className={favorite ? "fill-current" : ""} />
              </button>
            ) : null}

            <Segmented>
              <ToolbarBtn onClick={onUndo} disabled={!canUndo} title="Undo">
                <Undo2 size={16} />
              </ToolbarBtn>
              <ToolbarBtn onClick={onRedo} disabled={!canRedo} title="Redo">
                <Redo2 size={16} />
              </ToolbarBtn>
            </Segmented>

            <ZoomControl zoom={zoom} onZoomChange={onZoomChange} />

            <button
              type="button"
              onClick={onExport}
              className="flex h-10 items-center gap-2 rounded-xl bg-[#087bff] px-4 text-sm font-semibold text-white shadow-lg shadow-[#087bff]/25 hover:bg-[#1e88ff]"
            >
              <Download size={15} />
              <span className="hidden sm:inline">Export</span>
            </button>
          </div>
        </>
      )}
    </header>
  );
}

function Segmented({ children }: { children: ReactNode }) {
  return (
    <div className="flex h-10 items-center gap-0.5 rounded-xl border border-white/7 bg-white/8 p-0.5">
      {children}
    </div>
  );
}

function ZoomControl({
  zoom,
  onZoomChange,
}: {
  zoom: number;
  onZoomChange: (z: number) => void;
}) {
  return (
    <div className="flex h-10 items-center gap-2.5 rounded-xl border border-white/7 bg-white/8 px-3">
      <input
        type="range"
        min={25}
        max={200}
        value={zoom}
        onChange={(e) => onZoomChange(Number(e.target.value))}
        className="w-20 accent-[#d8e7ff] sm:w-24"
      />
      <span className="w-9 text-right text-xs tabular-nums text-white/55">{zoom}%</span>
    </div>
  );
}

function ToolbarBtn({
  children,
  onClick,
  disabled,
  title,
}: {
  children: ReactNode;
  onClick?: () => void;
  disabled?: boolean;
  title?: string;
}) {
  return (
    <button
      type="button"
      title={title}
      onClick={onClick}
      disabled={disabled}
      className="flex h-9 w-9 items-center justify-center rounded-lg text-white/55 transition hover:bg-white/10 hover:text-white disabled:opacity-30"
    >
      {children}
    </button>
  );
}
