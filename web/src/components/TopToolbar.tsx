import {
  ChevronDown,
  Download,
  Grid3x3,
  Redo2,
  Search,
  Undo2,
  ZoomIn,
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
}: TopToolbarProps) {
  return (
    <header className="glass flex h-14 shrink-0 items-center gap-3 border-b border-white/8 px-4">
      <div className="relative min-w-[280px] flex-1 max-w-md">
        <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-white/35" />
        <input
          type="search"
          placeholder="Search your photos"
          className="w-full rounded-xl border border-white/10 bg-black/25 py-2 pr-3 pl-9 text-sm text-white placeholder:text-white/35 outline-none focus:border-[#3b9bff]/50 focus:ring-1 focus:ring-[#3b9bff]/30"
        />
        <kbd className="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 rounded border border-white/10 bg-white/5 px-1.5 py-0.5 text-[10px] text-white/35">
          ⌘F
        </kbd>
      </div>

      <div className="relative">
        <select
          value={mode}
          onChange={(e) => onModeChange(e.target.value as AppMode)}
          className="appearance-none rounded-lg border border-white/10 bg-white/5 py-2 pr-8 pl-3 text-sm text-white outline-none focus:border-[#3b9bff]/50"
        >
          <option value="library">Library</option>
          <option value="edit">Edit</option>
        </select>
        <ChevronDown size={14} className="pointer-events-none absolute right-2 top-1/2 -translate-y-1/2 text-white/40" />
      </div>

      <ToolbarBtn onClick={onUndo} disabled={!canUndo} title="Undo">
        <Undo2 size={16} />
      </ToolbarBtn>
      <ToolbarBtn onClick={onRedo} disabled={!canRedo} title="Redo">
        <Redo2 size={16} />
      </ToolbarBtn>

      <div className="mx-1 h-6 w-px bg-white/10" />

      <ToolbarBtn active title="Grid view">
        <Grid3x3 size={16} />
      </ToolbarBtn>

      <div className="flex items-center gap-2 rounded-lg border border-white/10 bg-white/5 px-2 py-1">
        <ZoomIn size={14} className="text-white/40" />
        <input
          type="range"
          min={25}
          max={200}
          value={zoom}
          onChange={(e) => onZoomChange(Number(e.target.value))}
          className="w-20 accent-[#3b9bff]"
        />
        <span className="w-10 text-right text-xs tabular-nums text-white/60">{zoom}%</span>
      </div>

      {statusText ? (
        <span
          className="ml-auto max-w-[220px] truncate text-[11px] text-white/40"
          title={statusText}
        >
          {statusText}
        </span>
      ) : null}

      <button
        type="button"
        onClick={onExport}
        className={`flex items-center gap-2 rounded-xl bg-[#3b9bff] px-4 py-2 text-sm font-medium text-white shadow-lg shadow-[#3b9bff]/25 hover:bg-[#4aa5ff] ${statusText ? "" : "ml-auto"}`}
      >
        <Download size={16} />
        Export
      </button>
    </header>
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
      className={`rounded-lg border p-2 transition ${
        active
          ? "border-[#3b9bff]/50 bg-[#3b9bff]/15 text-white"
          : "border-white/10 bg-white/5 text-white/60 hover:bg-white/10 hover:text-white disabled:opacity-30"
      }`}
    >
      {children}
    </button>
  );
}
