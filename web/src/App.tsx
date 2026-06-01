import { useCallback, useEffect, useState } from "react";
import { EditingCanvas } from "./components/EditingCanvas";
import { HostPhotoImage } from "./components/HostPhotoImage";
import { RightEditPanel } from "./components/RightEditPanel";
import { Sidebar } from "./components/Sidebar";
import { TopToolbar } from "./components/TopToolbar";
import { useEditHistory } from "./hooks/useEditHistory";
import { isLumenHost } from "./lumen/hostBridge";
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
    alert(
      library.host
        ? `Export "${photo.title}" — coming soon (edits still preview-only in web UI).`
        : `Export "${photo.title}" with current edit settings (demo).`,
    );
  }, [photo, library.host]);

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
    if (library.host && photo.path) {
      void library.setFavorite(photo.path, next);
    }
  }, [photo, library, updatePhoto]);

  const sidebar = (
    <Sidebar
      totalCount={library.totalCount}
      favoriteCount={library.favoriteCount}
      folders={library.folders}
      view={library.view}
      selectedFolderPath={library.selectedFolderPath}
      host={library.host}
      onSelectAll={library.selectAllPhotos}
      onSelectFavorites={library.selectFavorites}
      onSelectFolder={library.selectFolder}
      onAddFolder={() => void library.addFolder()}
    />
  );

  if (library.host && library.loading && photos.length === 0) {
    return (
      <div className="flex h-full w-full overflow-hidden bg-[#080c12]">
        {sidebar}
        <div className="flex flex-1 items-center justify-center text-sm text-white/50">
          {library.statusText || "Loading library…"}
        </div>
      </div>
    );
  }

  if (library.host && !library.loading && photos.length === 0) {
    return (
      <div className="flex h-full w-full overflow-hidden bg-[#080c12]">
        {sidebar}
        <div className="flex flex-1 flex-col items-center justify-center gap-3 px-8 text-center">
          <p className="text-lg text-white/80">
            {library.view === "favorites"
              ? "No favorites yet — open a photo and tap the star."
              : library.selectedFolderPath
                ? "No photos in this folder"
                : "No photos in library"}
          </p>
          <p className="max-w-md text-sm text-white/45">{library.statusText}</p>
          {!library.selectedFolderPath && library.view !== "favorites" ? (
            <button
              type="button"
              className="rounded-lg bg-[#3b9bff] px-4 py-2 text-sm font-medium text-white hover:bg-[#2f8ef0]"
              onClick={() => void library.addFolder()}
            >
              Add folder…
            </button>
          ) : null}
        </div>
      </div>
    );
  }

  if (!photo) {
    return (
      <div className="flex h-full w-full items-center justify-center bg-[#080c12] text-sm text-white/50">
        No photos available
      </div>
    );
  }

  return (
    <div className="flex h-full w-full overflow-hidden bg-[#080c12]">
      {sidebar}

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
          statusText={library.host ? library.statusText : undefined}
        />

        {mode === "edit" ? (
          <div className="flex min-h-0 min-w-0 flex-1 overflow-hidden">
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
            <EditingCanvas
              photo={photo}
              photos={photos}
              edits={edits}
              zoom={zoom}
              onSelectPhoto={setSelectedId}
              onToggleFavorite={handleToggleFavorite}
              onToggleFlag={() => updatePhoto({ flagged: !photo.flagged })}
              onCopyEdits={handleCopyEdits}
            />
          </div>
        ) : (
          <LibraryGrid
            sections={sections}
            selectedId={selectedId}
            onSelect={(id) => {
              setSelectedId(id);
              setMode("edit");
              setZoom(100);
            }}
          />
        )}
      </div>

      {showCopiedToast ? (
        <div className="pointer-events-none fixed bottom-6 left-1/2 z-50 -translate-x-1/2 rounded-full border border-[#3b9bff]/40 bg-[#0f141c]/95 px-4 py-2 text-xs text-[#9ec9ff] shadow-xl backdrop-blur">
          Edit settings copied to clipboard
        </div>
      ) : null}

      {!library.host && !isLumenHost() ? (
        <div className="pointer-events-none fixed bottom-3 right-3 z-40 rounded-lg border border-white/10 bg-black/60 px-3 py-1.5 text-[10px] text-white/40">
          Browser demo — run via Lumen desktop for real photos
        </div>
      ) : null}
    </div>
  );
}

function LibraryGrid({
  sections,
  selectedId,
  onSelect,
}: {
  sections: { title: string; photos: PhotoItem[] }[];
  selectedId: string;
  onSelect: (id: string) => void;
}) {
  return (
    <div className="flex-1 overflow-y-auto p-6">
      {sections.map((section) => (
        <section key={section.title} className="mb-8">
          <h2 className="mb-4 text-lg font-semibold text-white/90">{section.title}</h2>
          <div className="flex flex-wrap gap-3">
            {section.photos.map((p) => (
              <button
                key={p.id}
                type="button"
                onClick={() => onSelect(p.id)}
                className={`h-40 w-52 overflow-hidden rounded-xl border transition hover:scale-[1.02] ${
                  p.id === selectedId
                    ? "border-[#3b9bff] ring-2 ring-[#3b9bff]/50"
                    : "border-white/10 hover:border-white/25"
                }`}
              >
                {p.path ? (
                  <HostPhotoImage
                    path={p.path}
                    alt={p.title}
                    className="h-full w-full object-cover"
                  />
                ) : (
                  <img src={p.src} alt="" className="h-full w-full object-cover" />
                )}
              </button>
            ))}
          </div>
        </section>
      ))}
    </div>
  );
}
