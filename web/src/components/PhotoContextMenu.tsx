import { FolderOpen, FolderSearch } from "lucide-react";
import type { ComponentType } from "react";
import { useEffect, useRef } from "react";
import { createPortal } from "react-dom";

export interface PhotoContextMenuState {
  x: number;
  y: number;
  photoId: string;
}

interface PhotoContextMenuProps {
  menu: PhotoContextMenuState | null;
  host?: boolean;
  onClose: () => void;
  onShowInFolder: (photoId: string) => void;
  onRevealInFileManager?: (photoId: string) => void;
}

export function PhotoContextMenu({
  menu,
  host = false,
  onClose,
  onShowInFolder,
  onRevealInFileManager,
}: PhotoContextMenuProps) {
  const panelRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menu) return;

    const onPointerDown = (event: MouseEvent) => {
      if (panelRef.current?.contains(event.target as Node)) return;
      onClose();
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };

    window.addEventListener("mousedown", onPointerDown);
    window.addEventListener("scroll", onClose, true);
    window.addEventListener("keydown", onKeyDown);
    return () => {
      window.removeEventListener("mousedown", onPointerDown);
      window.removeEventListener("scroll", onClose, true);
      window.removeEventListener("keydown", onKeyDown);
    };
  }, [menu, onClose]);

  if (!menu) return null;

  const revealLabel =
    typeof navigator !== "undefined" && /Mac|iPhone|iPad/.test(navigator.platform)
      ? "Reveal in Finder"
      : "Show in Explorer";

  const panelWidth = 220;
  const panelHeight = host && onRevealInFileManager ? 88 : 44;
  const x = Math.min(menu.x, window.innerWidth - panelWidth - 8);
  const y = Math.min(menu.y, window.innerHeight - panelHeight - 8);

  return createPortal(
    <div
      ref={panelRef}
      className="fixed z-[100] min-w-[220px] overflow-hidden rounded-xl border border-white/10 bg-[#121c2a]/98 py-1 shadow-2xl shadow-black/45 backdrop-blur-xl"
      style={{ left: x, top: y }}
      role="menu"
    >
      <MenuItem
        icon={FolderSearch}
        label="Show in folder"
        onClick={() => {
          onShowInFolder(menu.photoId);
          onClose();
        }}
      />
      {host && onRevealInFileManager ? (
        <MenuItem
          icon={FolderOpen}
          label={revealLabel}
          onClick={() => {
            onRevealInFileManager(menu.photoId);
            onClose();
          }}
        />
      ) : null}
    </div>,
    document.body,
  );
}

function MenuItem({
  icon: Icon,
  label,
  onClick,
}: {
  icon: ComponentType<{ size?: number; className?: string }>;
  label: string;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      role="menuitem"
      onClick={onClick}
      className="flex w-full items-center gap-2.5 px-3.5 py-2.5 text-left text-sm text-white/85 transition hover:bg-white/8"
    >
      <Icon size={15} className="shrink-0 text-white/45" />
      {label}
    </button>
  );
}
