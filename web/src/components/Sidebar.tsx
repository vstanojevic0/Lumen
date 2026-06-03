import {
  Folder,
  FolderOpen,
  Images,
  Plus,
  type LucideIcon,
} from "lucide-react";
import type { LibraryView, WebFolderDto } from "../lumen/hostBridge";

export function Sidebar({
  totalCount = 0,
  folders = [],
  view = "all",
  selectedFolderPath = null,
  host = false,
  onSelectAll,
  onSelectFolder,
  onAddFolder,
}: {
  totalCount?: number;
  folders?: WebFolderDto[];
  view?: LibraryView;
  selectedFolderPath?: string | null;
  host?: boolean;
  onSelectAll?: () => void;
  onSelectFolder?: (path: string) => void;
  onAddFolder?: () => void;
}) {
  const allActive = view === "all" && !selectedFolderPath;

  return (
    <aside className="lumen-sidebar flex h-full w-[284px] shrink-0 flex-col border-r border-white/8">
      <div className="px-5 pb-4 pt-5">
        <div className="mb-7 flex gap-2">
          <span className="h-3.5 w-3.5 rounded-full bg-[#ff5f57]" />
          <span className="h-3.5 w-3.5 rounded-full bg-[#febc2e]" />
          <span className="h-3.5 w-3.5 rounded-full bg-[#28c840]" />
        </div>

        <div className="flex items-center gap-3">
          <div className="h-10 w-10 rounded-full bg-[radial-gradient(circle_at_50%_38%,#ffd36f_0_18%,#ff6f61_19%_32%,#2f8cff_33%_58%,#183552_59%)] shadow-lg shadow-[#2f8cff]/25" />
          <div className="text-[28px] font-bold leading-none tracking-tight text-white">Lumen</div>
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto px-4 pb-4">
        <nav className="space-y-1">
          <NavButton
            icon={Images}
            label="All Photos"
            active={allActive}
            count={formatCount(totalCount, host)}
            onClick={onSelectAll}
          />
        </nav>

        <div className="mt-5 mb-2 flex items-center justify-between px-2">
          <span className="text-[11px] font-semibold uppercase tracking-wide text-white/42">
            Folders
          </span>
          {host && onAddFolder ? (
            <button
              type="button"
              onClick={onAddFolder}
              className="flex h-6 w-6 items-center justify-center rounded-lg text-white/60 hover:bg-white/8 hover:text-white"
              title="Add folder"
            >
              <Plus size={16} />
            </button>
          ) : null}
        </div>

        {folders.length > 0 ? (
          <FolderTree
            nodes={folders}
            selectedFolderPath={selectedFolderPath}
            onSelectFolder={onSelectFolder}
          />
        ) : host ? (
          <p className="px-2 text-xs text-white/35">No folders with photos yet.</p>
        ) : null}
      </div>

      <div className="px-5 pb-5 pt-4">
        <div className="mb-3 text-xs text-white/55">Local desktop library</div>
        <div className="flex items-center justify-between">
          <span className="text-xs text-white/42">{totalCount.toLocaleString()} photos indexed</span>
        </div>
      </div>
    </aside>
  );
}

function NavButton({
  icon: Icon,
  label,
  active = false,
  count,
  onClick,
}: {
  icon: LucideIcon;
  label: string;
  active?: boolean;
  count?: string | number;
  onClick?: () => void;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex w-full items-center gap-3 rounded-xl px-3 py-2 text-[13px] transition ${
        active
          ? "bg-[#2f8cff]/35 text-white shadow-[inset_0_0_0_1px_rgb(111_183_255_/_0.16)]"
          : "text-white/73 hover:bg-white/8 hover:text-white"
      }`}
    >
      <Icon size={17} className={active ? "text-white" : "text-white/58"} />
      <span className="min-w-0 flex-1 truncate text-left">{label}</span>
      {count !== undefined ? (
        <span className="text-xs tabular-nums text-white/52">
          {typeof count === "number" ? count.toLocaleString() : count}
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
    <div className="space-y-1">
      {nodes.map((node) => {
        const active = selectedFolderPath === node.path;
        const hasChildren = node.children.length > 0;
        return (
          <div key={node.path}>
            <button
              type="button"
              onClick={() => onSelectFolder?.(node.path)}
              className={`flex w-full items-center gap-2 rounded-xl py-1.5 text-[13px] transition ${
                active
                  ? "bg-white/12 text-white"
                  : "text-white/68 hover:bg-white/8 hover:text-white"
              }`}
              style={{ paddingLeft: 12 + depth * 16, paddingRight: 10 }}
            >
              {hasChildren ? (
                <FolderOpen size={16} className="shrink-0 text-white/48" />
              ) : (
                <Folder size={16} className="shrink-0 text-white/48" />
              )}
              <span className="min-w-0 flex-1 truncate text-left">{node.title}</span>
              <span className="text-xs tabular-nums text-white/38">
                {node.photoCount.toLocaleString()}
              </span>
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
    </div>
  );
}

function formatCount(value: number, host: boolean) {
  if (value > 0) return value.toLocaleString();
  return host ? "0" : undefined;
}
