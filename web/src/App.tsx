import { useCallback, useEffect, useState } from "react";
import { EditingCanvas } from "./components/EditingCanvas";
import { RightEditPanel } from "./components/RightEditPanel";
import { Sidebar } from "./components/Sidebar";
import { TopToolbar } from "./components/TopToolbar";
import { samplePhotos } from "./data/photos";
import { useEditHistory } from "./hooks/useEditHistory";
import { presets, type PresetId } from "./lib/presets";
import { defaultEditValues, type AppMode, type PhotoItem } from "./types";

export default function App() {
  const [mode, setMode] = useState<AppMode>("edit");
  const [photos, setPhotos] = useState<PhotoItem[]>(samplePhotos);
  const [selectedId, setSelectedId] = useState(samplePhotos[0].id);
  const [zoom, setZoom] = useState(100);
  const [showCopiedToast, setShowCopiedToast] = useState(false);

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

  const photo = photos.find((p) => p.id === selectedId) ?? photos[0];

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
    alert(`Export "${photo.title}" with current edit settings (demo).`);
  }, [photo.title]);

  const updatePhoto = useCallback(
    (patch: Partial<PhotoItem>) => {
      setPhotos((list) =>
        list.map((p) => (p.id === selectedId ? { ...p, ...patch } : p)),
      );
    },
    [selectedId],
  );

  return (
    <div className="flex h-full w-full overflow-hidden bg-[#080c12]">
      <Sidebar />

      <div className="flex min-w-0 flex-1 flex-col">
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
        />

        {mode === "edit" ? (
          <div className="flex min-h-0 flex-1">
            <EditingCanvas
              photo={photo}
              photos={photos}
              edits={edits}
              zoom={zoom}
              onSelectPhoto={setSelectedId}
              onRating={(rating) => updatePhoto({ rating })}
              onToggleFavorite={() => updatePhoto({ favorite: !photo.favorite })}
              onToggleFlag={() => updatePhoto({ flagged: !photo.flagged })}
              onCopyEdits={handleCopyEdits}
            />
            <RightEditPanel
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
          <LibraryGrid
            photos={photos}
            selectedId={selectedId}
            onSelect={(id) => {
              setSelectedId(id);
              setMode("edit");
            }}
          />
        )}
      </div>

      {showCopiedToast ? (
        <div className="pointer-events-none fixed bottom-6 left-1/2 z-50 -translate-x-1/2 rounded-full border border-[#3b9bff]/40 bg-[#0f141c]/95 px-4 py-2 text-xs text-[#9ec9ff] shadow-xl backdrop-blur">
          Edit settings copied to clipboard
        </div>
      ) : null}
    </div>
  );
}

function LibraryGrid({
  photos,
  selectedId,
  onSelect,
}: {
  photos: PhotoItem[];
  selectedId: string;
  onSelect: (id: string) => void;
}) {
  return (
    <div className="flex-1 overflow-y-auto p-6">
      <h2 className="mb-4 text-lg font-semibold text-white/90">May 2026</h2>
      <div className="flex flex-wrap gap-3">
        {photos.map((p) => (
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
            <img src={p.src} alt="" className="h-full w-full object-cover" />
          </button>
        ))}
      </div>
    </div>
  );
}
