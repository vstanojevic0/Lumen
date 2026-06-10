import {
  ChevronDown,
  Folder,
  Images,
  Plus,
  Star,
  type LucideIcon,
} from "lucide-react";
import { useEffect, useMemo, useRef, useState } from "react";
import { normalizeFolderPath } from "../lumen/folderScroll";
import {
  buildSidebarFromSections,
  folderPathsEqual,
  type GallerySectionLike,
} from "../lumen/sidebarFolderGroups";
import type { LibraryView, WebFolderDto } from "../lumen/hostBridge";

export function Sidebar({
  totalCount = 0,
  favoriteCount = 0,
  sections = [],
  scanRoots = [],
  view = "all",
  activeFolderPath = null,
  host = false,
  statusText,
  isBusy = false,
  onSelectAll,
  onSelectFavorites,
  onSelectFolder,
  onAddFolder,
}: {
  totalCount?: number;
  favoriteCount?: number;
  sections?: GallerySectionLike[];
  scanRoots?: WebFolderDto[];
  view?: LibraryView;
  activeFolderPath?: string | null;
  host?: boolean;
  statusText?: string;
  isBusy?: boolean;
  onSelectAll?: () => void;
  onSelectFavorites?: () => void;
  onSelectFolder?: (path: string) => void;
  onAddFolder?: () => void;
}) {
  const [foldersOpen, setFoldersOpen] = useState(true);
  const folderButtonRefs = useRef(new Map<string, HTMLButtonElement>());
  const allActive = view === "all" && !activeFolderPath;
  const favoritesActive = view === "favorites";
  const folderGroups = useMemo(
    () => buildSidebarFromSections(sections, scanRoots),
    [sections, scanRoots],
  );

  useEffect(() => {
    if (!activeFolderPath) return;
    const key = normalizeFolderPath(activeFolderPath).toLowerCase();
    const button = folderButtonRefs.current.get(key);
    button?.scrollIntoView({ block: "nearest", behavior: "smooth" });
  }, [activeFolderPath]);

  return (
    <aside
      className="lumen-sidebar flex h-full w-[284px] shrink-0 flex-col border-r border-white/8"
      data-lumen-sidebar
      onWheel={(e) => e.stopPropagation()}
    >
      <div className="px-4 pb-3 pt-5">
        <div className="mb-5 flex items-center gap-3 px-1">
          <div className="h-10 w-10 rounded-full bg-[radial-gradient(circle_at_50%_38%,#ffd36f_0_18%,#ff6f61_19%_32%,#2f8cff_33%_58%,#183552_59%)] shadow-lg shadow-[#2f8cff]/25" />
          <div className="text-[28px] font-bold leading-none tracking-tight text-white">Lumen</div>
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto px-2 pb-4">
        <nav className="space-y-px">
          <NavButton
            icon={Images}
            label="All Photos"
            active={allActive}
            count={formatCount(totalCount, host)}
            onClick={onSelectAll}
          />
          <NavButton
            icon={Star}
            label="Favorites"
            active={favoritesActive}
            count={favoriteCount > 0 ? favoriteCount : host ? 0 : undefined}
            iconClassName={favoritesActive ? "fill-[#f5c842] text-[#f5c842]" : "text-[#f5c842]/70"}
            onClick={onSelectFavorites}
          />
        </nav>

        <div className="mt-4">
          <div className="lumen-sidebar-section-header">
            <button
              type="button"
              onClick={() => setFoldersOpen((open) => !open)}
              className="flex min-w-0 flex-1 items-center gap-2"
            >
              <ChevronDown
                size={14}
                className={`shrink-0 text-[#75c9a3] transition-transform duration-200 ${
                  foldersOpen ? "" : "-rotate-90"
                }`}
              />
              <span className="truncate text-left">Folders</span>
            </button>
            {host && onAddFolder ? (
              <button
                type="button"
                onClick={onAddFolder}
                className="flex h-6 w-6 shrink-0 items-center justify-center rounded text-white/55 hover:bg-white/8 hover:text-white"
                title="Add folder"
              >
                <Plus size={15} />
              </button>
            ) : null}
          </div>

          {foldersOpen ? (
            <div className="mt-1">
              {folderGroups.length > 0 ? (
                folderGroups.map((group, index) => (
                  <div key={group.key}>
                    {index > 0 ? <div className="lumen-sidebar-year-rule" /> : null}
                    <div className="lumen-sidebar-year-label">{group.label}</div>
                    <div className="space-y-px">
                      {group.folders.map((folder) => {
                        const active = folderPathsEqual(folder.path, activeFolderPath);
                        const refKey = normalizeFolderPath(folder.path).toLowerCase();
                        return (
                          <button
                            key={folder.path}
                            ref={(element) => {
                              if (element) folderButtonRefs.current.set(refKey, element);
                              else folderButtonRefs.current.delete(refKey);
                            }}
                            type="button"
                            onClick={() => onSelectFolder?.(folder.path)}
                            className={`lumen-sidebar-folder ${active ? "lumen-sidebar-folder--active" : ""}`}
                            title={folder.title}
                          >
                            <Folder
                              size={16}
                              className={`shrink-0 ${active ? "text-[#f0d060]" : "text-[#d4b84a]/88"}`}
                              fill={active ? "currentColor" : "none"}
                            />
                            <span className="min-w-0 flex-1 truncate text-left uppercase tracking-[0.06em]">
                              {folder.title}
                            </span>
                            <span className="shrink-0 tabular-nums text-white/42">
                              ({folder.photoCount.toLocaleString()})
                            </span>
                          </button>
                        );
                      })}
                    </div>
                  </div>
                ))
              ) : host ? (
                <p className="px-3 py-2 text-xs text-white/35">No folders with photos yet.</p>
              ) : null}
            </div>
          ) : null}
        </div>
      </div>

      <div className="border-t border-white/6 px-4 py-3">
        <span className="block truncate text-[11px] text-white/38" title={isBusy ? statusText : undefined}>
          {isBusy && statusText
            ? statusText
            : `${totalCount.toLocaleString()} photos indexed`}
        </span>
      </div>
    </aside>
  );
}

function NavButton({
  icon: Icon,
  label,
  active = false,
  count,
  iconClassName,
  onClick,
}: {
  icon: LucideIcon;
  label: string;
  active?: boolean;
  count?: string | number;
  iconClassName?: string;
  onClick?: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`lumen-sidebar-folder ${active ? "lumen-sidebar-folder--active" : ""}`}
    >
      <Icon
        size={16}
        className={iconClassName ?? (active ? "text-white" : "text-white/55")}
      />
      <span className="min-w-0 flex-1 truncate text-left">{label}</span>
      {count !== undefined ? (
        <span className="shrink-0 tabular-nums text-white/42">
          {typeof count === "number" ? `(${count.toLocaleString()})` : count}
        </span>
      ) : null}
    </button>
  );
}

function formatCount(value: number, host: boolean) {
  if (value > 0) return value;
  return host ? 0 : undefined;
}
