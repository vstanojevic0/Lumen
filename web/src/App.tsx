import { Heart } from "lucide-react";
import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  findActiveFolderFromScroll,
  findJumpSectionPath,
  normalizeFolderPath,
} from "./lumen/folderScroll";
import { EditingCanvas } from "./components/EditingCanvas";
import { HostPhotoImage } from "./components/HostPhotoImage";
import { RightEditPanel } from "./components/RightEditPanel";
import { Sidebar } from "./components/Sidebar";
import { TopToolbar } from "./components/TopToolbar";
import { useEditHistory } from "./hooks/useEditHistory";
import { useEditNavigation } from "./hooks/useEditNavigation";
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
              sections={sections}
              selectedId={selectedId}
              zoom={zoom}
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
  zoom,
  folderJumpTarget,
  onActiveFolderChange,
  onJumpComplete,
  onSelect,
}: {
  sections: { title: string; folderPath: string; photos: PhotoItem[] }[];
  selectedId: string;
  zoom: number;
  folderJumpTarget: string | null;
  onActiveFolderChange: (path: string | null) => void;
  onJumpComplete: () => void;
  onSelect: (id: string) => void;
}) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const sectionRefs = useRef(new Map<string, HTMLElement>());
  const markerRefs = useRef(new Map<string, HTMLElement>());
  const scrollSpyPausedRef = useRef(false);
  const forcedFolderRef = useRef<string | null>(null);
  const activeFolderRef = useRef<string | null>(null);
  const [flashFolderPath, setFlashFolderPath] = useState<string | null>(null);

  const registerSectionRef = useCallback(
    (folderPath: string, element: HTMLElement | null) => {
      if (element) sectionRefs.current.set(folderPath, element);
      else sectionRefs.current.delete(folderPath);
    },
    [],
  );

  const registerMarkerRef = useCallback(
    (folderPath: string, element: HTMLElement | null) => {
      if (element) markerRefs.current.set(folderPath, element);
      else markerRefs.current.delete(folderPath);
    },
    [],
  );

  const publishActiveFolder = useCallback(
    (path: string | null) => {
      if (!path) return;
      const normalized = normalizeFolderPath(path).toLowerCase();
      if (activeFolderRef.current === normalized) return;
      activeFolderRef.current = normalized;
      onActiveFolderChange(path);
    },
    [onActiveFolderChange],
  );

  useEffect(() => {
    if (!folderJumpTarget) {
      scrollSpyPausedRef.current = false;
    }
  }, [folderJumpTarget]);

  useEffect(() => {
    activeFolderRef.current = null;
  }, [sections]);

  useEffect(() => {
    const container = scrollRef.current;
    if (!container || sections.length === 0) return;

    let frame = 0;

    const updateActiveFolder = () => {
      if (scrollSpyPausedRef.current) return;
      if (forcedFolderRef.current) {
        publishActiveFolder(forcedFolderRef.current);
        return;
      }
      const active = findActiveFolderFromScroll(
        sections,
        container,
        markerRefs.current,
        sectionRefs.current,
      );
      publishActiveFolder(active);
    };

    const onScroll = () => {
      if (!forcedFolderRef.current) {
        scrollSpyPausedRef.current = false;
      }
      if (frame) return;
      frame = window.requestAnimationFrame(() => {
        frame = 0;
        updateActiveFolder();
      });
    };

    const observer = new IntersectionObserver(
      () => updateActiveFolder(),
      {
        root: container,
        rootMargin: "-72px 0px -55% 0px",
        threshold: [0, 0.01, 0.1, 0.25, 0.5, 1],
      },
    );

    const observeMarkers = () => {
      observer.disconnect();
      for (const section of sections) {
        const marker = markerRefs.current.get(section.folderPath);
        if (marker) observer.observe(marker);
      }
      updateActiveFolder();
    };

    observeMarkers();
    const markerTimer = window.setTimeout(observeMarkers, 0);

    container.addEventListener("scroll", onScroll, { passive: true });
    window.addEventListener("resize", onScroll);

    return () => {
      window.clearTimeout(markerTimer);
      observer.disconnect();
      container.removeEventListener("scroll", onScroll);
      window.removeEventListener("resize", onScroll);
      if (frame) window.cancelAnimationFrame(frame);
    };
  }, [sections, publishActiveFolder]);

  useEffect(() => {
    const container = scrollRef.current;
    if (!container || !folderJumpTarget) return;

    scrollSpyPausedRef.current = true;

    const finishJump = (path: string | null) => {
      if (path) {
        activeFolderRef.current = null;
        forcedFolderRef.current = path;
        publishActiveFolder(path);
      }
      scrollSpyPausedRef.current = false;
      forcedFolderRef.current = null;
      onJumpComplete();
    };

    if (folderJumpTarget === "__top__") {
      const firstPath = sections[0]?.folderPath ?? null;
      forcedFolderRef.current = firstPath;
      activeFolderRef.current = null;
      publishActiveFolder(firstPath);
      container.scrollTo({ top: 0, behavior: "smooth" });

      const topTimer = window.setTimeout(() => finishJump(firstPath), 700);
      return () => window.clearTimeout(topTimer);
    }

    const sectionPath = findJumpSectionPath(sections, folderJumpTarget);
    if (!sectionPath) {
      finishJump(null);
      return;
    }

    const element = sectionRefs.current.get(sectionPath);
    if (!element) {
      finishJump(sectionPath);
      return;
    }

    forcedFolderRef.current = sectionPath;
    activeFolderRef.current = null;
    publishActiveFolder(sectionPath);
    setFlashFolderPath(sectionPath);
    element.scrollIntoView({ behavior: "smooth", block: "start" });

    const flashTimer = window.setTimeout(() => setFlashFolderPath(null), 1200);

    const onScrollEnd = () => finishJump(sectionPath);
    container.addEventListener("scrollend", onScrollEnd, { once: true });

    const resumeTimer = window.setTimeout(() => {
      container.removeEventListener("scrollend", onScrollEnd);
      finishJump(sectionPath);
    }, 1100);

    return () => {
      container.removeEventListener("scrollend", onScrollEnd);
      window.clearTimeout(flashTimer);
      window.clearTimeout(resumeTimer);
    };
  }, [folderJumpTarget, sections, publishActiveFolder, onJumpComplete]);

  if (sections.length === 0) {
    return (
      <div className="flex flex-1 items-center justify-center px-8 text-center">
        <div className="lumen-surface max-w-sm rounded-2xl px-8 py-7">
          <div className="text-sm font-medium text-white/80">Your library is empty.</div>
        </div>
      </div>
    );
  }

  const tileMin = Math.round(96 + zoom * 0.6);

  return (
    <div ref={scrollRef} className="min-w-0 flex-1 overflow-y-auto px-7 py-5 scroll-smooth" data-photo-scroll-container>
      {sections.map((section, index) => (
        <section
          key={section.folderPath}
          ref={(element) => registerSectionRef(section.folderPath, element)}
          data-folder-path={section.folderPath}
          className={`lumen-folder-section ${index === 0 ? "" : "mt-2"} ${
            flashFolderPath === section.folderPath ? "lumen-folder-section--flash" : ""
          }`}
        >
          {index > 0 ? (
            <div
              ref={(element) => registerMarkerRef(section.folderPath, element)}
              data-folder-marker
              className="lumen-folder-divider mb-4 mt-1"
            >
              <span className="lumen-folder-divider__label">{section.title}</span>
            </div>
          ) : (
            <div
              ref={(element) => registerMarkerRef(section.folderPath, element)}
              data-folder-marker
              className="mb-3 flex items-center gap-2"
            >
              <span className="text-[11px] font-medium uppercase tracking-wide text-white/38">
                {section.title}
              </span>
              <span className="h-px flex-1 bg-white/8" />
            </div>
          )}
          <div
            className="grid gap-2.5 pb-5"
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

