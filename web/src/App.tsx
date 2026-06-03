import {
  Calendar,
  Camera,
  Heart,
  Info,
  MoreHorizontal,
  SearchX,
  Share2,
  Star,
} from "lucide-react";
import { useCallback, useEffect, useMemo, useState, type ReactNode } from "react";
import { EditingCanvas } from "./components/EditingCanvas";
import { HostPhotoImage } from "./components/HostPhotoImage";
import { RightEditPanel } from "./components/RightEditPanel";
import { Sidebar } from "./components/Sidebar";
import { TopToolbar } from "./components/TopToolbar";
import { useEditHistory } from "./hooks/useEditHistory";
import { filmstripWindow } from "./lumen/mediaUrls";
import { useLumenLibrary } from "./lumen/useLumenLibrary";
import { presets, type PresetId } from "./lib/presets";
import { defaultEditValues, type AppMode, type PhotoItem } from "./types";

export default function App() {
  const library = useLumenLibrary();
  const [mode, setMode] = useState<AppMode>("library");
  const [photos, setPhotos] = useState<PhotoItem[]>(library.photos);
  const [sections, setSections] = useState(library.sections);
  const [selectedId, setSelectedId] = useState("");
  const [zoom, setZoom] = useState(100);
  const [showCopiedToast, setShowCopiedToast] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");

  useEffect(() => {
    setPhotos(library.photos);
    setSections(library.sections);
    if (library.photos.length > 0 && !library.photos.some((p) => p.id === selectedId)) {
      setSelectedId(library.photos[0].id);
    }
  }, [library.photos, library.sections, selectedId]);

  const {
    edits,
    activePreset,
    update,
    replace,
    reset,
    undo,
    redo,
    canUndo,
    canRedo,
  } = useEditHistory();

  const photo =
    photos.find((p) => p.id === selectedId) ?? photos[0] ?? null;

  const filmstripPhotos = useMemo(
    () => (photo ? filmstripWindow(photos, photo.id) : []),
    [photos, photo],
  );

  const visibleSections = useMemo(() => {
    const query = searchQuery.trim().toLowerCase();
    if (!query) return sections;

    return sections
      .map((section) => ({
        ...section,
        photos: section.photos.filter((p) =>
          [p.title, p.path].filter(Boolean).some((value) =>
            value?.toLowerCase().includes(query),
          ),
        ),
      }))
      .filter((section) => section.photos.length > 0);
  }, [sections, searchQuery]);

  const viewLabel = useMemo(() => {
    if (library.selectedFolderPath) {
      return folderNameFromPath(library.selectedFolderPath);
    }

    return "All Photos";
  }, [library.selectedFolderPath]);

  const applyPreset = useCallback(
    (id: PresetId) => {
      const base = defaultEditValues();
      const patch = presets[id].patch;
      replace({ ...base, ...patch }, id);
    },
    [replace],
  );

  const handleCopyEdits = useCallback(() => {
    void navigator.clipboard.writeText(JSON.stringify(edits, null, 2));
    setShowCopiedToast(true);
  }, [edits]);

  useEffect(() => {
    if (!showCopiedToast) return;
    const t = window.setTimeout(() => setShowCopiedToast(false), 2200);
    return () => window.clearTimeout(t);
  }, [showCopiedToast]);

  const handleExport = useCallback(() => {
    if (!photo) return;
    alert(`Export "${photo.title}" — coming soon in the desktop web UI.`);
  }, [photo]);

  const updatePhoto = useCallback(
    (patch: Partial<PhotoItem>) => {
      if (!selectedId) return;
      setPhotos((list) =>
        list.map((p) => (p.id === selectedId ? { ...p, ...patch } : p)),
      );
    },
    [selectedId],
  );

  const handleToggleFavorite = useCallback(() => {
    if (!photo) return;
    const next = !photo.favorite;
    updatePhoto({ favorite: next });
    if (photo.path) {
      void library.setFavorite(photo.path, next);
    }
  }, [photo, library, updatePhoto]);

  const sidebar = (
    <Sidebar
      totalCount={library.totalCount}
      folders={library.folders}
      view={library.view}
      selectedFolderPath={library.selectedFolderPath}
      host={library.host}
      onSelectAll={library.selectAllPhotos}
      onSelectFolder={library.selectFolder}
      onAddFolder={() => void library.addFolder()}
    />
  );

  if (!library.host) {
    return <DesktopOnlySplash readyChecked={library.hostChecked} />;
  }

  if (library.loading && photos.length === 0) {
    return (
      <AppShell sidebar={sidebar}>
        <div className="flex flex-1 items-center justify-center text-sm text-white/52">
          {library.statusText || "Loading library..."}
        </div>
      </AppShell>
    );
  }

  if (!library.loading && photos.length === 0) {
    return (
      <AppShell sidebar={sidebar}>
        <EmptyLibraryState
          title={
            library.view === "favorites"
              ? "No favorites yet"
              : library.selectedFolderPath
                ? "No photos in this folder"
                : "No photos in library"
          }
          detail={library.statusText}
          showAddFolder={!library.selectedFolderPath && library.view !== "favorites"}
          onAddFolder={() => void library.addFolder()}
        />
      </AppShell>
    );
  }

  if (!photo) {
    return (
      <AppShell sidebar={sidebar}>
        <EmptyLibraryState title="No photos available" detail={library.statusText} />
      </AppShell>
    );
  }

  return (
    <AppShell sidebar={sidebar}>
      <div className="flex min-h-0 min-w-0 flex-1 flex-col overflow-hidden">
        <TopToolbar
          mode={mode}
          onModeChange={setMode}
          zoom={zoom}
          onZoomChange={setZoom}
          canUndo={canUndo}
          canRedo={canRedo}
          onUndo={undo}
          onRedo={redo}
          onExport={handleExport}
          statusText={library.statusText}
          viewLabel={viewLabel}
          searchQuery={searchQuery}
          onSearchQueryChange={setSearchQuery}
        />

        {mode === "edit" ? (
          <div className="flex min-h-0 min-w-0 flex-1 overflow-hidden">
            <EditingCanvas
              photo={photo}
              filmstripPhotos={filmstripPhotos}
              edits={edits}
              zoom={zoom}
              onSelectPhoto={setSelectedId}
              onToggleFavorite={handleToggleFavorite}
              onToggleFlag={() => updatePhoto({ flagged: !photo.flagged })}
              onCopyEdits={handleCopyEdits}
            />
            <RightEditPanel
              photo={photo}
              edits={edits}
              activePreset={activePreset as PresetId | null}
              onChange={update}
              onApplyPreset={applyPreset}
              onReset={reset}
              onCopyEdits={handleCopyEdits}
              onExport={handleExport}
            />
          </div>
        ) : (
          <div className="flex min-h-0 min-w-0 flex-1 overflow-hidden">
            <LibraryGrid
              sections={visibleSections}
              selectedId={selectedId}
              searchQuery={searchQuery}
              zoom={zoom}
              onSelect={(id) => {
                setSelectedId(id);
                setMode("edit");
                setZoom(100);
              }}
            />
            <LibraryInspector
              photo={photo}
              edits={edits}
              onToggleFavorite={handleToggleFavorite}
              onExport={handleExport}
              onOpenEdit={() => setMode("edit")}
            />
          </div>
        )}
      </div>

      {showCopiedToast ? (
        <div className="pointer-events-none fixed bottom-6 left-1/2 z-50 -translate-x-1/2 rounded-full border border-[#2f8cff]/40 bg-[#152131]/95 px-4 py-2 text-xs text-[#b8d8ff] shadow-xl backdrop-blur">
          Edit settings copied to clipboard
        </div>
      ) : null}
    </AppShell>
  );
}

function AppShell({
  sidebar,
  children,
}: {
  sidebar: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="lumen-app-shell flex h-full w-full overflow-hidden">
      {sidebar}
      {children}
    </div>
  );
}

function DesktopOnlySplash({ readyChecked }: { readyChecked: boolean }) {
  return (
    <div className="lumen-app-shell flex h-full w-full items-center justify-center px-8 text-center">
      <div className="max-w-sm rounded-2xl border border-white/10 bg-white/[0.07] px-8 py-7 shadow-2xl shadow-black/30 backdrop-blur">
        <div className="mx-auto mb-4 h-12 w-12 rounded-2xl bg-[radial-gradient(circle_at_50%_38%,#ffd36f_0_18%,#ff6f61_19%_32%,#2f8cff_33%_58%,#183552_59%)] shadow-lg shadow-[#2f8cff]/30" />
        <div className="text-xl font-semibold text-white">Lumen</div>
        <p className="mt-2 text-sm leading-6 text-white/58">
          {readyChecked
            ? "This interface runs inside the Lumen desktop app."
            : "Starting the desktop photo workspace..."}
        </p>
      </div>
    </div>
  );
}

function EmptyLibraryState({
  title,
  detail,
  showAddFolder = false,
  onAddFolder,
}: {
  title: string;
  detail?: string;
  showAddFolder?: boolean;
  onAddFolder?: () => void;
}) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-3 px-8 text-center">
      <p className="text-lg font-medium text-white/82">{title}</p>
      {detail ? <p className="max-w-md text-sm text-white/45">{detail}</p> : null}
      {showAddFolder ? (
        <button
          type="button"
          className="rounded-xl bg-[#2f8cff] px-4 py-2 text-sm font-semibold text-white shadow-lg shadow-[#2f8cff]/25 hover:bg-[#4a9dff]"
          onClick={onAddFolder}
        >
          Add folder
        </button>
      ) : null}
    </div>
  );
}

function LibraryGrid({
  sections,
  selectedId,
  searchQuery,
  zoom,
  onSelect,
}: {
  sections: { title: string; photos: PhotoItem[] }[];
  selectedId: string;
  searchQuery: string;
  zoom: number;
  onSelect: (id: string) => void;
}) {
  if (sections.length === 0) {
    return (
      <div className="flex flex-1 items-center justify-center px-8 text-center">
        <div className="lumen-surface max-w-sm rounded-2xl px-8 py-7">
          <SearchX size={28} className="mx-auto mb-3 text-white/35" />
          <div className="text-sm font-medium text-white/80">No matching photos</div>
          <div className="mt-1 text-xs text-white/42">
            {searchQuery ? `Nothing found for "${searchQuery}".` : "Your library is empty."}
          </div>
        </div>
      </div>
    );
  }

  const tileMin = Math.round(96 + zoom * 0.6);

  return (
    <div className="min-w-0 flex-1 overflow-y-auto px-7 py-5">
      {sections.map((section) => (
        <section key={`${section.title}-${section.photos[0]?.id ?? "empty"}`} className="mb-8">
          <div className="mb-3 flex items-end gap-3">
            <h2 className="text-[17px] font-semibold text-white/92">{section.title}</h2>
            <span className="pb-0.5 text-xs font-medium text-[#5da2ff]">
              {section.photos.length.toLocaleString()} Photos
            </span>
          </div>
          <div
            className="grid gap-2.5"
            style={{ gridTemplateColumns: `repeat(auto-fill, minmax(${tileMin}px, 1fr))` }}
          >
            {section.photos.map((p) => (
              <button
                key={p.id}
                type="button"
                onClick={() => onSelect(p.id)}
                className={`group relative aspect-square overflow-hidden rounded-lg border bg-white/5 text-left transition duration-200 hover:-translate-y-0.5 hover:border-white/22 hover:shadow-2xl hover:shadow-black/25 ${
                  p.id === selectedId
                    ? "border-[#1e88ff] ring-2 ring-[#1e88ff]/80"
                    : "border-white/8"
                }`}
              >
                <HostPhotoImage
                  path={p.path}
                  alt={p.title}
                  className="h-full w-full object-cover"
                />
                <div className="pointer-events-none absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/68 via-black/20 to-transparent p-2.5 opacity-0 transition group-hover:opacity-100">
                  <div className="truncate text-xs font-medium text-white/92">{p.title}</div>
                </div>
                {p.favorite ? (
                  <Heart
                    size={16}
                    className="absolute right-2 top-2 fill-[#ff625b] text-[#ff625b] drop-shadow"
                  />
                ) : null}
              </button>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}

function LibraryInspector({
  photo,
  edits,
  onToggleFavorite,
  onExport,
  onOpenEdit,
}: {
  photo: PhotoItem;
  edits: ReturnType<typeof defaultEditValues>;
  onToggleFavorite: () => void;
  onExport: () => void;
  onOpenEdit: () => void;
}) {
  return (
    <aside className="glass hidden h-full w-[344px] shrink-0 flex-col border-l border-white/8 p-3 xl:flex">
      <div className="mb-3 flex items-center justify-between">
        <div className="rounded-full bg-white/6 p-1 text-xs font-medium text-white/62">
          <button type="button" className="rounded-full bg-white/10 px-5 py-1.5 text-white">
            Info
          </button>
          <button
            type="button"
            onClick={onOpenEdit}
            className="rounded-full px-5 py-1.5 text-white/55 hover:text-white"
          >
            Edit
          </button>
        </div>
        <button
          type="button"
          className="flex h-9 w-9 items-center justify-center rounded-xl bg-white/6 text-white/55"
          title="More"
        >
          <MoreHorizontal size={18} />
        </button>
      </div>

      <div className="overflow-hidden rounded-xl border border-white/8 bg-white/5">
        <div className="relative aspect-[4/3]">
          <HostPhotoImage
            path={photo.path}
            alt={photo.title}
            maxEdge="preview"
            eager
            className="h-full w-full object-cover"
          />
          <button
            type="button"
            onClick={onToggleFavorite}
            className={`absolute right-3 top-3 text-[#ff625b] drop-shadow ${
              photo.favorite ? "" : "opacity-75"
            }`}
            title={photo.favorite ? "Remove from favorites" : "Add to favorites"}
          >
            <Heart size={20} className={photo.favorite ? "fill-current" : ""} />
          </button>
        </div>
      </div>

      <div className="mt-4">
        <div className="flex items-center justify-between gap-3">
          <h2 className="min-w-0 truncate text-lg font-semibold text-white">{photo.title}</h2>
          {isRawLike(photo.title) ? (
            <span className="rounded-md bg-white/10 px-2 py-1 text-[10px] font-bold text-white/70">
              RAW
            </span>
          ) : null}
        </div>
        <div className="mt-1 text-xs text-white/45">Selected photo</div>
      </div>

      <div className="mt-4 grid grid-cols-4 gap-2 rounded-xl border border-white/8 bg-white/5 p-2">
        <Meta icon={Camera} label="Lens" value={photo.focalLength} />
        <Meta icon={Calendar} label="Shutter" value={photo.shutter} />
        <Meta icon={Info} label="Aperture" value={photo.aperture} />
        <Meta icon={Star} label="ISO" value={photo.iso > 0 ? String(photo.iso) : "—"} />
      </div>

      <div className="mt-4 rounded-xl border border-white/8 bg-white/5 p-3">
        <div className="mb-3 flex items-center justify-between text-sm font-semibold text-white/85">
          <span>Quick Edits</span>
          <span className="text-xs font-normal text-white/38">Preview</span>
        </div>
        <InspectorSlider label="Exposure" value={edits.exposure} min={-2} max={2} />
        <InspectorSlider label="Contrast" value={edits.contrast} min={-100} max={100} />
        <InspectorSlider label="Highlights" value={edits.highlights} min={-100} max={100} />
        <InspectorSlider label="Shadows" value={edits.shadows} min={-100} max={100} />
        <InspectorSlider label="Vibrance" value={edits.vibrance} min={-100} max={100} />
        <InspectorSlider label="Saturation" value={edits.saturation} min={-100} max={100} />
      </div>

      <div className="mt-auto grid grid-cols-2 gap-3 pt-4">
        <button
          type="button"
          className="flex items-center justify-center gap-2 rounded-xl bg-white/10 px-4 py-3 text-sm font-medium text-white/80 hover:bg-white/14"
        >
          <Share2 size={16} />
          Share
        </button>
        <button
          type="button"
          onClick={onExport}
          className="rounded-xl bg-[#087bff] px-4 py-3 text-sm font-semibold text-white shadow-lg shadow-[#087bff]/25 hover:bg-[#1e88ff]"
        >
          Export...
        </button>
      </div>
    </aside>
  );
}

function Meta({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof Camera;
  label: string;
  value: string;
}) {
  return (
    <div className="min-w-0 text-center">
      <Icon size={14} className="mx-auto mb-1 text-white/45" />
      <div className="truncate text-[10px] text-white/36">{label}</div>
      <div className="truncate text-[10px] font-medium text-white/72">{value}</div>
    </div>
  );
}

function InspectorSlider({
  label,
  value,
  min,
  max,
}: {
  label: string;
  value: number;
  min: number;
  max: number;
}) {
  const pct = ((value - min) / (max - min)) * 100;
  return (
    <div className="mb-3 grid grid-cols-[82px_1fr_42px] items-center gap-3 text-xs last:mb-0">
      <span className="text-white/68">{label}</span>
      <div className="h-1.5 rounded-full bg-white/10">
        <div
          className="h-full rounded-full bg-white/45"
          style={{ width: `${Math.max(0, Math.min(100, pct))}%` }}
        />
      </div>
      <span className="text-right tabular-nums text-white/58">{formatEditValue(value)}</span>
    </div>
  );
}

function isRawLike(name: string) {
  return /\.(raw|dng|cr2|cr3|nef|arw|orf|raf|rw2)$/i.test(name);
}

function formatEditValue(value: number) {
  if (Math.abs(value) < 0.005) return "0";
  return value > 0 ? `+${Math.round(value)}` : String(Math.round(value));
}

function folderNameFromPath(path: string) {
  const trimmed = path.replace(/[\\/]+$/, "");
  return trimmed.split(/[\\/]/).pop() || trimmed || "Folder";
}
