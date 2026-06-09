import { isFolderUnderRoot, normalizeFolderPath } from "./folderScroll";
import type { WebFolderDto } from "./hostBridge";

export type SidebarFolderItem = {
  path: string;
  title: string;
  photoCount: number;
};

export type SidebarFolderGroup = {
  key: string;
  label: string;
  folders: SidebarFolderItem[];
};

export type GallerySectionLike = {
  title: string;
  folderPath: string;
  photos: unknown[];
};

function findScanRootLabel(folderPath: string, scanRoots: WebFolderDto[]): string {
  const path = normalizeFolderPath(folderPath);
  let best: WebFolderDto | null = null;
  let bestLength = -1;

  for (const root of scanRoots) {
    const rootPath = normalizeFolderPath(root.path);
    if (!isFolderUnderRoot(path, rootPath)) continue;
    if (rootPath.length > bestLength) {
      best = root;
      bestLength = rootPath.length;
    }
  }

  return best?.title ?? "Library";
}

/**
 * Sidebar folder list mirrors gallery section order exactly (single source of truth).
 */
export function buildSidebarFromSections(
  sections: GallerySectionLike[],
  scanRoots: WebFolderDto[],
): SidebarFolderGroup[] {
  const groups: SidebarFolderGroup[] = [];

  for (const section of sections) {
    const groupLabel = findScanRootLabel(section.folderPath, scanRoots);
    const item: SidebarFolderItem = {
      path: section.folderPath,
      title: section.title,
      photoCount: section.photos.length,
    };

    const last = groups[groups.length - 1];
    if (last?.key === groupLabel) {
      last.folders.push(item);
      continue;
    }

    groups.push({
      key: groupLabel,
      label: groupLabel,
      folders: [item],
    });
  }

  return groups;
}

export function folderPathsEqual(a: string | null, b: string | null): boolean {
  if (!a || !b) return false;
  return normalizeFolderPath(a).toLowerCase() === normalizeFolderPath(b).toLowerCase();
}
