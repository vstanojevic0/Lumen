import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { EditingCanvas } from "./components/EditingCanvas";
import { RightEditPanel } from "./components/RightEditPanel";
import { Sidebar } from "./components/Sidebar";
import { TopToolbar } from "./components/TopToolbar";
import { VirtualLibraryGrid } from "./components/VirtualLibraryGrid";
import { useEditHistory } from "./hooks/useEditHistory";
import { useEditNavigation } from "./hooks/useEditNavigation";
import { usePhotoNavigation } from "./hooks/usePhotoNavigation";
import { filmstripWindow } from "./lumen/mediaUrls";
import { useLumenLibrary } from "./lumen/useLumenLibrary";
import { presets, type PresetId } from "./lib/presets";
import { rotateOrientation } from "./lib/rotation";
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

  const { enterEdit, backToLibrary, handleModeChange } = useEditNavigation(
    mode,
    setMode,
  );

  const photo =
    photos.find((p) => p.id === selectedId) ?? photos[0] ?? null;

  const filmstripPhotos = useMemo(
    () => (photo ? filmstripWindow(photos, photo.id) : []),
    [photos, photo],
  );

  const navigationPhotos = photos;

  const handleSelectPhoto = useCallback(
    (id: string) => {
      setSelectedId(id);
      reset();
    },
    [reset],
  );

  usePhotoNavigation({
    photos: navigationPhotos,
    selectedId,
    onSelect: handleSelectPhoto,
    enabled: navigationPhotos.length > 1 && Boolean(selectedId),
    allowWheel: mode === "edit",
  });

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

  const handleRotateLeft = useCallback(() => {
    update("orientation", rotateOrientation(edits.orientation, -90));
  }, [edits.orientation, update]);

  const handleRotateRight = useCallback(() => {
    update("orientation", rotateOrientation(edits.orientation, 90));
  }, [edits.orientation, update]);

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
      sections={sections}
      scanRoots={library.folders}
      view={library.view}
      activeFolderPath={library.activeFolderPath}
      host={library.host}
      onSelectAll={library.selectAllPhotos}
      onSelectFolder={library.jumpToFolder}
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
              : "No photos in library"
          }
          detail={library.statusText}
          showAddFolder={library.view !== "favorites"}
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
          onModeChange={handleModeChange}
          zoom={zoom}
          onZoomChange={setZoom}
          canUndo={canUndo}
          canRedo={canRedo}
          onUndo={undo}
          onRedo={redo}
          onExport={handleExport}
          onBack={mode === "edit" ? backToLibrary : undefined}
          statusText={library.statusText}
        />

        {mode === "edit" ? (
          <div className="flex min-h-0 min-w-0 flex-1 overflow-hidden">
            <EditingCanvas
              photo={photo}
              filmstripPhotos={filmstripPhotos}
              edits={edits}
              zoom={zoom}
              onSelectPhoto={handleSelectPhoto}
              onToggleFavorite={handleToggleFavorite}
              onToggleFlag={() => updatePhoto({ flagged: !photo.flagged })}
              onCopyEdits={handleCopyEdits}
              onRotateLeft={handleRotateLeft}
              onRotateRight={handleRotateRight}
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
        ) : null}

        <div
          className={`min-h-0 min-w-0 flex-1 overflow-hidden ${mode === "library" ? "flex" : "hidden"}`}
          aria-hidden={mode !== "library"}
        >
          <VirtualLibraryGrid
            sections={sections}
            selectedId={selectedId}
            zoom={zoom}
            libraryVisible={mode === "library"}
            folderJumpTarget={library.folderJumpTarget}
            onActiveFolderChange={library.setActiveFolderPath}
            onJumpComplete={library.clearFolderJumpTarget}
            onSelect={(id) => {
              handleSelectPhoto(id);
              enterEdit();
              setZoom(100);
            }}
          />
        </div>
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

