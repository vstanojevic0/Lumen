import { Heart } from "lucide-react";
import { useVirtualizer } from "@tanstack/react-virtual";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { findJumpSectionPath, findSectionPathForPhotoId } from "../lumen/folderScroll";
import type { PhotoItem } from "../types";
import { HostPhotoImage } from "./HostPhotoImage";

export type GallerySection = {
  title: string;
  folderPath: string;
  photos: PhotoItem[];
};

type VirtualRow =
  | { kind: "header"; key: string; folderPath: string; title: string; isFirst: boolean }
  | { kind: "tiles"; key: string; folderPath: string; photos: PhotoItem[]; tileSize: number };

const GRID_GAP = 10;
const GRID_PADDING_X = 28;
const HEADER_HEIGHT = 44;
const DIVIDER_HEADER_HEIGHT = 52;

function buildRows(sections: GallerySection[], columnCount: number, tileSize: number): VirtualRow[] {
  const rows: VirtualRow[] = [];

  sections.forEach((section, index) => {
    rows.push({
      kind: "header",
      key: `h:${section.folderPath}`,
      folderPath: section.folderPath,
      title: section.title,
      isFirst: index === 0,
    });

    for (let i = 0; i < section.photos.length; i += columnCount) {
      rows.push({
        kind: "tiles",
        key: `t:${section.folderPath}:${i}`,
        folderPath: section.folderPath,
        photos: section.photos.slice(i, i + columnCount),
        tileSize,
      });
    }
  });

  return rows;
}

function rowHeight(row: VirtualRow): number {
  if (row.kind === "header") {
    return row.isFirst ? HEADER_HEIGHT : DIVIDER_HEADER_HEIGHT;
  }
  return row.tileSize + GRID_GAP;
}

export function VirtualLibraryGrid({
  sections,
  selectedId,
  zoom,
  libraryVisible,
  folderJumpTarget,
  onSelect,
  onActiveFolderChange,
  onJumpComplete,
}: {
  sections: GallerySection[];
  selectedId: string;
  zoom: number;
  libraryVisible: boolean;
  folderJumpTarget: string | null;
  onSelect: (id: string) => void;
  onActiveFolderChange: (path: string | null) => void;
  onJumpComplete: () => void;
}) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const wasLibraryVisibleRef = useRef(libraryVisible);
  const [containerWidth, setContainerWidth] = useState(800);

  const tileMin = Math.round(96 + zoom * 0.6);
  const columnCount = Math.max(
    1,
    Math.floor((containerWidth - GRID_PADDING_X) / (tileMin + GRID_GAP)),
  );
  const tileSize = Math.floor(
    (containerWidth - GRID_PADDING_X - GRID_GAP * (columnCount - 1)) / columnCount,
  );

  const rows = useMemo(
    () => buildRows(sections, columnCount, tileSize),
    [sections, columnCount, tileSize],
  );

  const virtualizer = useVirtualizer({
    count: rows.length,
    getScrollElement: () => scrollRef.current,
    estimateSize: (index) => rowHeight(rows[index]),
    overscan: 6,
    measureElement: (element) => element.getBoundingClientRect().height,
  });

  useEffect(() => {
    const container = scrollRef.current;
    if (!container) return;

    const observer = new ResizeObserver(([entry]) => {
      setContainerWidth(entry.contentRect.width);
    });
    observer.observe(container);
    setContainerWidth(container.clientWidth);
    return () => observer.disconnect();
  }, []);

  useEffect(() => {
    if (!libraryVisible) return;
    virtualizer.measure();
  }, [libraryVisible, columnCount, tileSize, virtualizer, rows.length]);

  const publishActiveFolder = useCallback(
    (path: string | null) => {
      if (path) onActiveFolderChange(path);
    },
    [onActiveFolderChange],
  );

  useEffect(() => {
    const container = scrollRef.current;
    if (!container || rows.length === 0) return;

    let frame = 0;
    const updateActiveFolder = () => {
      const items = virtualizer.getVirtualItems();
      if (items.length === 0) return;

      for (const item of items) {
        const row = rows[item.index];
        if (!row) continue;
        publishActiveFolder(row.folderPath);
        return;
      }
    };

    const onScroll = () => {
      if (frame) return;
      frame = window.requestAnimationFrame(() => {
        frame = 0;
        updateActiveFolder();
      });
    };

    container.addEventListener("scroll", onScroll, { passive: true });
    updateActiveFolder();
    return () => {
      container.removeEventListener("scroll", onScroll);
      if (frame) window.cancelAnimationFrame(frame);
    };
  }, [rows, virtualizer, publishActiveFolder]);

  useEffect(() => {
    const wasVisible = wasLibraryVisibleRef.current;
    wasLibraryVisibleRef.current = libraryVisible;
    if (wasVisible || !libraryVisible || !selectedId) return;

    const sectionPath = findSectionPathForPhotoId(sections, selectedId);
    if (sectionPath) publishActiveFolder(sectionPath);

    const rowIndex = rows.findIndex(
      (row) => row.kind === "tiles" && row.photos.some((photo) => photo.id === selectedId),
    );
    if (rowIndex >= 0) {
      virtualizer.scrollToIndex(rowIndex, { align: "center" });
    }
  }, [libraryVisible, selectedId, sections, rows, virtualizer, publishActiveFolder]);

  useEffect(() => {
    if (!folderJumpTarget) return;

    const finish = (path: string | null) => {
      if (path) publishActiveFolder(path);
      onJumpComplete();
    };

    if (folderJumpTarget === "__top__") {
      scrollRef.current?.scrollTo({ top: 0, behavior: "smooth" });
      const timer = window.setTimeout(
        () => finish(sections[0]?.folderPath ?? null),
        650,
      );
      return () => window.clearTimeout(timer);
    }

    const sectionPath = findJumpSectionPath(sections, folderJumpTarget);
    if (!sectionPath) {
      finish(null);
      return;
    }

    const rowIndex = rows.findIndex(
      (row) => row.kind === "header" && row.folderPath === sectionPath,
    );
    if (rowIndex >= 0) {
      virtualizer.scrollToIndex(rowIndex, { align: "start", behavior: "smooth" });
    }

    const timer = window.setTimeout(() => finish(sectionPath), 750);
    return () => window.clearTimeout(timer);
  }, [folderJumpTarget, rows, sections, virtualizer, publishActiveFolder, onJumpComplete]);

  if (sections.length === 0) {
    return (
      <div className="flex flex-1 items-center justify-center px-8 text-center">
        <div className="lumen-surface max-w-sm rounded-2xl px-8 py-7">
          <div className="text-sm font-medium text-white/80">Your library is empty.</div>
        </div>
      </div>
    );
  }

  return (
    <div
      ref={scrollRef}
      className="min-w-0 flex-1 overflow-y-auto px-7 py-5 scroll-smooth"
      data-photo-scroll-container
    >
      <div
        className="relative w-full"
        style={{ height: `${virtualizer.getTotalSize()}px` }}
      >
        {virtualizer.getVirtualItems().map((virtualRow) => {
          const row = rows[virtualRow.index];
          if (!row) return null;

          return (
            <div
              key={row.key}
              ref={virtualizer.measureElement}
              data-index={virtualRow.index}
              data-folder-path={row.folderPath}
              className="absolute left-0 top-0 w-full"
              style={{ transform: `translateY(${virtualRow.start}px)` }}
            >
              {row.kind === "header" ? (
                row.isFirst ? (
                  <div className="mb-3 flex items-center gap-2" data-folder-marker>
                    <span className="text-[11px] font-medium uppercase tracking-wide text-white/38">
                      {row.title}
                    </span>
                    <span className="h-px flex-1 bg-white/8" />
                  </div>
                ) : (
                  <div className="lumen-folder-divider mb-4 mt-1" data-folder-marker>
                    <span className="lumen-folder-divider__label">{row.title}</span>
                  </div>
                )
              ) : (
                <div
                  className="grid pb-2.5"
                  style={{
                    gridTemplateColumns: `repeat(${columnCount}, ${tileSize}px)`,
                    gap: `${GRID_GAP}px`,
                  }}
                >
                  {row.photos.map((photo) => (
                    <button
                      key={photo.id}
                      type="button"
                      onClick={() => onSelect(photo.id)}
                      className={`group relative overflow-hidden rounded-lg border bg-white/5 text-left transition duration-200 hover:-translate-y-0.5 hover:border-white/22 hover:shadow-2xl hover:shadow-black/25 ${
                        photo.id === selectedId
                          ? "border-[#1e88ff] ring-2 ring-[#1e88ff]/80"
                          : "border-white/8"
                      }`}
                      style={{ width: tileSize, height: tileSize }}
                    >
                      <HostPhotoImage
                        path={photo.path}
                        alt={photo.title}
                        className="h-full w-full object-cover"
                      />
                      <div className="pointer-events-none absolute inset-x-0 bottom-0 bg-gradient-to-t from-black/68 via-black/20 to-transparent p-2.5 opacity-0 transition group-hover:opacity-100">
                        <div className="truncate text-xs font-medium text-white/92">
                          {photo.title}
                        </div>
                      </div>
                      {photo.favorite ? (
                        <Heart
                          size={16}
                          className="absolute right-2 top-2 fill-[#ff625b] text-[#ff625b] drop-shadow"
                        />
                      ) : null}
                    </button>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
}
