export interface WebPhotoDto {
  path: string;
  title: string;
  favorite: boolean;
}

export interface WebFolderDto {
  path: string;
  title: string;
  photoCount: number;
  children: WebFolderDto[];
}

export interface WebGallerySectionDto {
  title: string;
  photos: WebPhotoDto[];
}

export interface WebGallerySnapshot {
  totalCount: number;
  statusText: string;
  isBusy: boolean;
  sections: WebGallerySectionDto[];
}

export interface WebStatusDto {
  totalCount: number;
  statusText: string;
  isBusy: boolean;
  favoriteCount: number;
  mediaBaseUrl?: string | null;
}

export type LibraryView = "all" | "favorites";

export interface GalleryQuery {
  folderPath?: string | null;
  favoritesOnly?: boolean;
}

export interface WebImageDto {
  url: string;
}

type LumenHost = {
  isAvailable: boolean;
  call<T>(method: string, params?: unknown): Promise<T>;
};

declare global {
  interface Window {
    __lumenHost?: LumenHost;
    __lumenMediaBase?: string;
  }
}

export function isLumenHost(): boolean {
  return Boolean(window.__lumenHost?.isAvailable);
}

export async function lumenCall<T>(method: string, params?: unknown): Promise<T> {
  const host = window.__lumenHost;
  if (!host?.isAvailable) {
    throw new Error("Lumen desktop host is not available.");
  }
  return host.call<T>(method, params);
}

export function onLumenEvent<T>(
  name: "status" | "libraryUpdated",
  handler: (detail: T) => void,
): () => void {
  const type = `lumen:${name}`;
  const listener = (e: Event) => handler((e as CustomEvent<T>).detail);
  window.addEventListener(type, listener);
  return () => window.removeEventListener(type, listener);
}
