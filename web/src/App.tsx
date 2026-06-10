import { useCallback, useEffect, useMemo, useState, type MouseEvent, type ReactNode } from "react";
import { EditingCanvas } from "./components/EditingCanvas";
import { PhotoContextMenu, type PhotoContextMenuState } from "./components/PhotoContextMenu";
import { RightEditPanel, type InspectorTab } from "./components/RightEditPanel";
import { Sidebar } from "./components/Sidebar";
import { TopToolbar } from "./components/TopToolbar";
import { VirtualLibraryGrid } from "./components/VirtualLibraryGrid";
import { useEditHistory } from "./hooks/useEditHistory";
import { useEditNavigation } from "./hooks/useEditNavigation";
import { useCtrlWheelZoom } from "./hooks/useCtrlWheelZoom";
import { usePhotoNavigation } from "./hooks/usePhotoNavigation";
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
  const [inspectorOpen, setInspectorOpen] = useState(true);
  const [inspectorTab, setInspectorTab] = useState<InspectorTab>("edit");
  const [photoContextMenu, setPhotoContextMenu] = useState<PhotoContextMenuState | null>(null);
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

  const { enterEdit, backToLibrary } = useEditNavigation(
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

  useCtrlWheelZoom(setZoom);

  const applyPreset = useCallback(
    (id: PresetId) => {
      const base = defaultEditValues();
      const patch = presets[id].patch;
      replace({ ...base, ...patch }, id);
    },
    [replace],
  );

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

  const openPhotoContextMenu = useCallback((photoId: string, event: MouseEvent) => {
    setPhotoContextMenu({
      x: event.clientX,
      y: event.clientY,
      photoId,
    });
  }, []);

  const handleShowInFolder = useCallback(
    (photoId: string) => {
      setSelectedId(photoId);
      library.showPhotoInFolder(photoId);
      if (mode === "edit") backToLibrary();
    },
    [library, mode, backToLibrary],
  );

  const handleRevealInFileManager = useCallback(
    (photoId: string) => {
      void library.revealPhotoInFileManager(photoId);
    },
    [library],
  );

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

  if (library.isLibraryPending) {
    return (
      <AppShell sidebar={sidebar}>
        <LibraryLoadingState statusText={library.statusText} />
      </AppShell>
    );
  }

  if (photos.length === 0) {
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
          photoTitle={photo.title}
          favorite={photo.favorite}
          onToggleFavorite={mode === "edit" ? handleToggleFavorite : undefined}
          inspectorOpen={inspectorOpen}
          onToggleInspector={mode === "edit" ? () => setInspectorOpen((v) => !v) : undefined}
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
          <div className="relative flex min-h-0 min-w-0 flex-1 overflow-hidden">
            <EditingCanvas
              photo={photo}
              filmstripPhotos={filmstripPhotos}
              edits={edits}
              zoom={zoom}
              onSelectPhoto={handleSelectPhoto}
              onPhotoContextMenu={openPhotoContextMenu}
            />
            {inspectorOpen ? (
              <RightEditPanel
                photo={photo}
                edits={edits}
                activePreset={activePreset as PresetId | null}
                tab={inspectorTab}
                onTabChange={setInspectorTab}
                onClose={() => setInspectorOpen(false)}
                onChange={update}
                onApplyPreset={applyPreset}
                onReset={reset}
                onExport={handleExport}
              />
            ) : null}
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
            libraryJumpTarget={library.libraryJumpTarget}
            onActiveFolderChange={library.setActiveFolderPath}
            onJumpComplete={library.clearLibraryJumpTarget}
            onPhotoContextMenu={openPhotoContextMenu}
            onSelect={(id) => {
              handleSelectPhoto(id);
              enterEdit();
              setZoom(100);
            }}
          />
        </div>
      </div>

      <PhotoContextMenu
        menu={photoContextMenu}
        host={library.host}
        onClose={() => setPhotoContextMenu(null)}
        onShowInFolder={handleShowInFolder}
        onRevealInFileManager={handleRevealInFileManager}
      />
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

function LibraryLoadingState({ statusText }: { statusText?: string }) {
  return (
    <div className="flex flex-1 flex-col items-center justify-center gap-4 px-8 text-center">
      <div className="h-9 w-9 animate-spin rounded-full border-2 border-white/12 border-t-[#2f8cff]" />
      <div>
        <p className="text-sm font-medium text-white/72">
          {statusText || "Loading library…"}
        </p>
        <p className="mt-1 text-xs text-white/38">This may take a moment on first launch.</p>
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

