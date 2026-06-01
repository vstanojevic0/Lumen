import {
  Folder,
  FolderOpen,
  Image,
  Settings,
  Star,
} from "lucide-react";
import type { LibraryView } from "../lumen/hostBridge";
import type { WebFolderDto } from "../lumen/hostBridge";

export function Sidebar({
  totalCount = 0,
  favoriteCount = 0,
  folders = [],
  view = "all",
  selectedFolderPath = null,
  host = false,
  onSelectAll,
  onSelectFavorites,
  onSelectFolder,
  onAddFolder,
}: {
  totalCount?: number;
  favoriteCount?: number;
  folders?: WebFolderDto[];
  view?: LibraryView;
  selectedFolderPath?: string | null;
  host?: boolean;
  onSelectAll?: () => void;
  onSelectFavorites?: () => void;
  onSelectFolder?: (path: string) => void;
  onAddFolder?: () => void;
}) {
  const allActive = view === "all" && !selectedFolderPath;
  const favoritesActive = view === "favorites";

  return (
    <aside className="glass flex h-full w-[248px] shrink-0 flex-col border-r border-white/8">
      <div className="border-b border-white/8 px-4 py-5">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-[#3b9bff] to-[#2a6fd4] text-lg font-bold text-white shadow-lg shadow-[#3b9bff]/25">
            L
          </div>
          <div>
            <div className="text-lg font-semibold tracking-tight">Lumen</div>
            <div className="text-[11px] text-white/45">Photo library</div>
          </div>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto px-2 py-3">
        <nav className="space-y-0.5">
          <NavButton
            icon={Image}
            label="All Photos"
            active={allActive}
            count={totalCount > 0 ? totalCount.toLocaleString() : host ? "0" : undefined}
            onClick={onSelectAll}
          />
          <NavButton
            icon={Star}
            label="Favorites"
            active={favoritesActive}
            count={favoriteCount > 0 ? favoriteCount.toLocaleString() : undefined}
            onClick={onSelectFavorites}
          />
        </nav>

        {folders.length > 0 ? (
          <>
            <SectionLabel>Folders</SectionLabel>
            <FolderTree
              nodes={folders}
              selectedFolderPath={selectedFolderPath}
              onSelectFolder={onSelectFolder}
            />
          </>
        ) : host ? (
          <p className="mt-6 px-3 text-xs text-white/35">No folders with photos yet.</p>
        ) : null}
      </div>

      <div className="border-t border-white/8 p-4">
        {host && onAddFolder ? (
          <button
            type="button"
            onClick={onAddFolder}
            className="flex w-full items-center justify-center gap-2 rounded-lg border border-[#3b9bff]/30 bg-[#3b9bff]/10 py-2 text-xs text-[#9ec9ff] hover:bg-[#3b9bff]/20"
          >
            <FolderOpen size={14} />
            Add folder…
          </button>
        ) : null}
        <button
          type="button"
          className="mt-2 flex w-full items-center justify-center gap-2 rounded-lg py-2 text-xs text-white/50 hover:bg-white/6 hover:text-white/80"
        >
          <Settings size={14} />
          Settings
        </button>
      </div>
    </aside>
  );
}

function NavButton({
  icon: Icon,
  label,
  active,
  count,
  onClick,
}: {
  icon: typeof Image;
  label: string;
  active: boolean;
  count?: string;
  onClick?: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-[13px] transition ${
        active
          ? "bg-[#3b9bff]/15 text-white ring-1 ring-[#3b9bff]/40"
          : "text-white/70 hover:bg-white/6 hover:text-white"
      }`}
    >
      <Icon size={16} className={active ? "text-[#7eb8ff]" : "text-white/45"} />
      <span className="flex-1 text-left">{label}</span>
      {count ? (
        <span className="rounded-md bg-white/8 px-1.5 py-0.5 text-[10px] text-white/55">
          {count}
        </span>
      ) : null}
    </button>
  );
}

function FolderTree({
  nodes,
  selectedFolderPath,
  onSelectFolder,
  depth = 0,
}: {
  nodes: WebFolderDto[];
  selectedFolderPath: string | null;
  onSelectFolder?: (path: string) => void;
  depth?: number;
}) {
  return (
    <>
      {nodes.map((node) => {
        const active = selectedFolderPath === node.path;
        const hasChildren = node.children.length > 0;
        return (
          <div key={node.path}>
            <button
              type="button"
              onClick={() => onSelectFolder?.(node.path)}
              className={`flex w-full items-center gap-2 rounded-lg py-1.5 text-[13px] transition ${
                active
                  ? "bg-white/10 text-white"
                  : "text-white/65 hover:bg-white/6 hover:text-white"
              }`}
              style={{ paddingLeft: 12 + depth * 14, paddingRight: 12 }}
            >
              {hasChildren ? (
                <FolderOpen size={14} className="shrink-0 text-white/40" />
              ) : (
                <Folder size={14} className="shrink-0 text-white/40" />
              )}
              <span className="min-w-0 flex-1 truncate text-left">{node.title}</span>
              {node.photoCount > 0 ? (
                <span className="shrink-0 text-[10px] text-white/35 tabular-nums">
                  {node.photoCount.toLocaleString()}
                </span>
              ) : null}
            </button>
            {hasChildren ? (
              <FolderTree
                nodes={node.children}
                selectedFolderPath={selectedFolderPath}
                onSelectFolder={onSelectFolder}
                depth={depth + 1}
              />
            ) : null}
          </div>
        );
      })}
    </>
  );
}

function SectionLabel({ children }: { children: string }) {
  return (
    <div className="mt-5 mb-1.5 px-3 text-[10px] font-semibold tracking-widest text-white/35 uppercase">
      {children}
    </div>
  );
}
