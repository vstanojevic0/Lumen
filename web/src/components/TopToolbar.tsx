import {
  ArrowLeft,
  Download,
  PanelRightClose,
  PanelRightOpen,
  Redo2,
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
  statusText?: string;
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
  statusText,
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
              className="flex h-9 items-center gap-2 rounded-xl border border-white/7 bg-white/8 px-3 text-sm font-medium text-white/88 transition hover:border-white/14 hover:bg-white/12"
              title="Back to library (Esc)"
            >
              <ArrowLeft size={16} className="text-white/65" />
              <span>Library</span>
            </button>
          ) : null}

          {photoTitle ? (
            <div
              className="hidden min-w-0 max-w-[min(28vw,320px)] truncate text-sm text-white/55 sm:block"
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
                className={`flex h-9 w-9 items-center justify-center rounded-xl border transition ${
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

            {onToggleInspector ? (
              <ToolbarBtn
                onClick={onToggleInspector}
                active={inspectorOpen}
                title={inspectorOpen ? "Hide info & edit panel" : "Show info & edit panel"}
              >
                {inspectorOpen ? <PanelRightClose size={16} /> : <PanelRightOpen size={16} />}
              </ToolbarBtn>
            ) : null}

            <ZoomControl zoom={zoom} onZoomChange={onZoomChange} />

            <button
              type="button"
              onClick={onExport}
              className="flex items-center gap-2 rounded-xl bg-[#087bff] px-3.5 py-2 text-sm font-semibold text-white shadow-lg shadow-[#087bff]/25 hover:bg-[#1e88ff]"
            >
              <Download size={15} />
              <span className="hidden sm:inline">Export</span>
            </button>
          </div>
        </>
      )}

      {statusText ? (
        <span
          className={`min-w-0 truncate text-xs text-white/35 ${
            mode === "library" ? "ml-3 max-w-[32vw]" : "max-w-[16vw]"
          }`}
          title={statusText}
        >
          {statusText}
        </span>
      ) : null}
    </header>
  );
}

function Segmented({ children }: { children: ReactNode }) {
  return (
    <div className="flex h-9 items-center gap-0.5 rounded-xl border border-white/7 bg-white/8 p-0.5">
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
    <div className="flex h-9 items-center gap-2.5 rounded-xl border border-white/7 bg-white/8 px-3">
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
  active,
  title,
}: {
  children: ReactNode;
  onClick?: () => void;
  disabled?: boolean;
  active?: boolean;
  title?: string;
}) {
  return (
    <button
      type="button"
      title={title}
      onClick={onClick}
      disabled={disabled}
      className={`flex h-8 w-8 items-center justify-center rounded-lg transition ${
        active
          ? "bg-[#087bff]/90 text-white"
          : "text-white/55 hover:bg-white/10 hover:text-white disabled:opacity-30"
      }`}
    >
      {children}
    </button>
  );
}
