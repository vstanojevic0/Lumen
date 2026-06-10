import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { PhotoItem } from "../types";
import { applyMediaBase } from "./mediaUrls";
import {
  isLumenHost,
  lumenCall,
  onLumenEvent,
  type GalleryQuery,
  type LibraryView,
  type WebFolderDto,
  type WebGallerySnapshot,
  type WebStatusDto,
} from "./hostBridge";

function mapSnapshot(snapshot: WebGallerySnapshot): {
  photos: PhotoItem[];
  sections: { title: string; folderPath: string; photos: PhotoItem[] }[];
} {
  const photos: PhotoItem[] = [];
  const sections = snapshot.sections.map((section) => {
    const sectionPhotos = section.photos.map((p) => {
      const item: PhotoItem = {
        id: p.path,
        title: p.title,
        src: "",
        path: p.path,
        iso: 0,
        focalLength: "—",
        aperture: "—",
        shutter: "—",
        favorite: p.favorite,
        flagged: false,
      };
      photos.push(item);
      return item;
    });
    return {
      title: section.title,
      folderPath: section.folderPath,
      photos: sectionPhotos,
    };
  });

  return { photos, sections };
}

export function useLumenLibrary() {
  const [host, setHost] = useState(() => isLumenHost());
  const [hostChecked, setHostChecked] = useState(() => isLumenHost());
  const [status, setStatus] = useState<WebStatusDto | null>(null);
  const [gallery, setGallery] = useState<WebGallerySnapshot | null>(null);
  const [folders, setFolders] = useState<WebFolderDto[]>([]);
  const [loading, setLoading] = useState(host);
  const [view, setView] = useState<LibraryView>("all");
  const [activeFolderPath, setActiveFolderPath] = useState<string | null>(null);
  const [folderJumpTarget, setFolderJumpTarget] = useState<string | null>(null);

  const galleryQuery = useMemo<GalleryQuery>(
    () => ({
      folderPath: null,
      favoritesOnly: view === "favorites",
    }),
    [view],
  );

  const galleryQueryRef = useRef(galleryQuery);
  galleryQueryRef.current = galleryQuery;

  useEffect(() => {
    if (host) {
      setHostChecked(true);
      return;
    }

    const syncHost = () => {
      if (isLumenHost()) {
        setHost(true);
        setHostChecked(true);
      }
    };

    syncHost();
    window.addEventListener("lumen:hostReady", syncHost);
    const interval = window.setInterval(syncHost, 100);
    const timeout = window.setTimeout(() => setHostChecked(true), 1400);

    return () => {
      window.removeEventListener("lumen:hostReady", syncHost);
      window.clearInterval(interval);
      window.clearTimeout(timeout);
    };
  }, [host]);

  const loadGallery = useCallback(
    async (query: GalleryQuery = galleryQuery) => {
      if (!host) return;
      const nextGallery = await lumenCall<WebGallerySnapshot>("getGallery", query);
      setGallery(nextGallery);
    },
    [host, galleryQuery],
  );

  const refresh = useCallback(async () => {
    if (!host) return;
    setLoading(true);
    try {
      const [nextStatus, nextFolders] = await Promise.all([
        lumenCall<WebStatusDto>("getStatus"),
        lumenCall<WebFolderDto[]>("getFolders"),
      ]);
      setStatus(nextStatus);
      setFolders(nextFolders);
      await loadGallery();
    } finally {
      setLoading(false);
    }
  }, [host, loadGallery]);

  useEffect(() => {
    if (!host) return;
    void refresh();
    const offStatus = onLumenEvent<WebStatusDto>("status", (next) => {
      applyMediaBase(next.mediaBaseUrl);
      setStatus(next);
    });
    const offLibrary = onLumenEvent("libraryUpdated", () => {
      void (async () => {
        const nextStatus = await lumenCall<WebStatusDto>("getStatus");
        applyMediaBase(nextStatus.mediaBaseUrl);
        setStatus(nextStatus);

        if (nextStatus.isBusy) return;

        const [nextFolders, nextGallery] = await Promise.all([
          lumenCall<WebFolderDto[]>("getFolders"),
          lumenCall<WebGallerySnapshot>("getGallery", galleryQueryRef.current),
        ]);
        setFolders(nextFolders);
        setGallery(nextGallery);
      })();
    });
    return () => {
      offStatus();
      offLibrary();
    };
  }, [host, refresh]);

  useEffect(() => {
    if (!host) return;
    void loadGallery(galleryQuery);
  }, [host, galleryQuery, loadGallery]);

  const selectAllPhotos = useCallback(() => {
    setView("all");
    setFolderJumpTarget("__top__");
  }, []);

  const selectFavorites = useCallback(() => {
    setView("favorites");
    setActiveFolderPath(null);
    setFolderJumpTarget(null);
  }, []);

  const jumpToFolder = useCallback((path: string) => {
    setView("all");
    setActiveFolderPath(path);
    setFolderJumpTarget(path);
  }, []);

  const clearFolderJumpTarget = useCallback(() => {
    setFolderJumpTarget(null);
  }, []);

  const setFavorite = useCallback(
    async (path: string, favorite: boolean) => {
      if (!host) return;
      await lumenCall("setFavorite", { path, favorite });
      setGallery((current) => {
        if (!current) return current;
        const sections = current.sections.map((section) => ({
          ...section,
          photos: section.photos.map((p) =>
            p.path === path ? { ...p, favorite } : p,
          ),
        }));
        return { ...current, sections };
      });
      const nextStatus = await lumenCall<WebStatusDto>("getStatus");
      setStatus(nextStatus);
      if (view === "favorites" && !favorite) {
        await loadGallery({ favoritesOnly: true });
      }
    },
    [host, view, loadGallery],
  );

  const mapped = useMemo(() => {
    if (gallery) return mapSnapshot(gallery);
    return {
      photos: [],
      sections: [],
    };
  }, [gallery]);

  return {
    host,
    hostChecked,
    loading,
    status,
    folders,
    view,
    activeFolderPath,
    folderJumpTarget,
    totalCount: status?.totalCount ?? gallery?.totalCount ?? mapped.photos.length,
    favoriteCount: status?.favoriteCount ?? 0,
    statusText: gallery?.statusText ?? status?.statusText ?? "",
    isBusy: status?.isBusy ?? gallery?.isBusy ?? false,
    photos: mapped.photos,
    sections: mapped.sections,
    selectAllPhotos,
    selectFavorites,
    jumpToFolder,
    setActiveFolderPath,
    clearFolderJumpTarget,
    setFavorite,
    refresh,
    rescan: () => lumenCall("rescan"),
    addFolder: () => lumenCall("addFolder"),
  };
}
