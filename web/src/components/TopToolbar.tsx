import {
  ChevronDown,
  Download,
  LayoutGrid,
  Maximize2,
  Redo2,
  Search,
  Undo2,
} from "lucide-react";
import type { ReactNode } from "react";
import type { AppMode } from "../types";

interface TopToolbarProps {
  mode: AppMode;
  onModeChange: (mode: AppMode) => void;
  zoom: number;
  onZoomChange: (z: number) => void;
  canUndo: boolean;
  canRedo: boolean;
  onUndo: () => void;
  onRedo: () => void;
  onExport: () => void;
  statusText?: string;
  viewLabel: string;
  searchQuery: string;
  onSearchQueryChange: (query: string) => void;
}

export function TopToolbar({
  mode,
  onModeChange,
  zoom,
  onZoomChange,
  canUndo,
  canRedo,
  onUndo,
  onRedo,
  onExport,
  statusText,
  viewLabel,
  searchQuery,
  onSearchQueryChange,
}: TopToolbarProps) {
  return (
    <header className="lumen-toolbar flex h-[112px] shrink-0 flex-col border-b border-white/8 px-6 py-3">
      <div className="flex min-w-0 items-center gap-4">
        <div className="relative w-[420px] max-w-[42vw]">
          <Search size={17} className="pointer-events-none absolute left-4 top-1/2 -translate-y-1/2 text-white/45" />
          <input
            type="search"
            placeholder="Search your photos"
            value={searchQuery}
            onChange={(e) => onSearchQueryChange(e.target.value)}
            className="h-10 w-full rounded-2xl border border-white/7 bg-white/8 pr-12 pl-11 text-sm text-white outline-none placeholder:text-white/42 focus:border-[#2f8cff]/60 focus:bg-white/10"
          />
          <kbd className="pointer-events-none absolute right-4 top-1/2 -translate-y-1/2 text-xs text-white/42">
            ⌘F
          </kbd>
        </div>

        {statusText ? (
          <span className="min-w-0 flex-1 truncate text-xs text-white/38" title={statusText}>
            {statusText}
          </span>
        ) : (
          <span className="flex-1" />
        )}

        {mode === "edit" ? (
          <button
            type="button"
            onClick={onExport}
            className="flex items-center gap-2 rounded-xl bg-[#087bff] px-4 py-2.5 text-sm font-semibold text-white shadow-lg shadow-[#087bff]/25 hover:bg-[#1e88ff]"
          >
            <Download size={16} />
            Export...
          </button>
        ) : null}
      </div>

      <div className="mt-4 flex min-w-0 items-center gap-3">
        <ModeSelect mode={mode} onModeChange={onModeChange} />

        {mode === "library" ? (
          <>
            <div className="flex h-10 items-center gap-2 rounded-xl border border-white/7 bg-white/9 px-3 text-sm font-medium text-white/82">
              <LayoutGrid size={16} className="text-white/62" />
              <span className="max-w-[240px] truncate">{viewLabel}</span>
            </div>
            <div className="ml-auto flex items-center gap-2">
              <Segmented>
                <ToolbarBtn active title="Grid view">
                  <LayoutGrid size={17} />
                </ToolbarBtn>
              </Segmented>
              <ZoomControl zoom={zoom} onZoomChange={onZoomChange} />
              <ToolbarBtn title="Fit">
                <Maximize2 size={16} />
              </ToolbarBtn>
            </div>
          </>
        ) : (
          <>
            <div className="ml-auto flex items-center gap-2">
              <Segmented>
                <ToolbarBtn onClick={onUndo} disabled={!canUndo} title="Undo">
                  <Undo2 size={16} />
                </ToolbarBtn>
                <ToolbarBtn onClick={onRedo} disabled={!canRedo} title="Redo">
                  <Redo2 size={16} />
                </ToolbarBtn>
              </Segmented>
              <ZoomControl zoom={zoom} onZoomChange={onZoomChange} />
              <ToolbarBtn title="Fit to screen">
                <Maximize2 size={16} />
              </ToolbarBtn>
            </div>
          </>
        )}
      </div>
    </header>
  );
}

function ModeSelect({
  mode,
  onModeChange,
}: {
  mode: AppMode;
  onModeChange: (mode: AppMode) => void;
}) {
  return (
    <div className="relative">
      <select
        value={mode}
        onChange={(e) => onModeChange(e.target.value as AppMode)}
        className="h-10 min-w-[156px] appearance-none rounded-xl border border-white/7 bg-white/10 py-2 pr-9 pl-4 text-sm font-medium text-white outline-none focus:border-[#2f8cff]/60"
      >
        <option value="library">All Photos</option>
        <option value="edit">Edit</option>
      </select>
      <ChevronDown size={15} className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-white/55" />
    </div>
  );
}

function Segmented({ children }: { children: ReactNode }) {
  return (
    <div className="flex h-10 items-center gap-1 rounded-xl border border-white/7 bg-white/8 p-1">
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
    <div className="flex h-10 items-center gap-3 rounded-xl border border-white/7 bg-white/8 px-3">
      <input
        type="range"
        min={25}
        max={200}
        value={zoom}
        onChange={(e) => onZoomChange(Number(e.target.value))}
        className="w-24 accent-[#d8e7ff]"
      />
      <span className="w-10 text-right text-xs tabular-nums text-white/62">{zoom}%</span>
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
          ? "bg-[#087bff] text-white shadow-lg shadow-[#087bff]/25"
          : "text-white/62 hover:bg-white/10 hover:text-white disabled:opacity-30"
      }`}
    >
      {children}
    </button>
  );
}
