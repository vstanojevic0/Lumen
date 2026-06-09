export type GallerySectionRef = {
  title: string;
  folderPath: string;
};

export function normalizeFolderPath(path: string): string {
  return path.replace(/[\\/]+$/, "").replace(/\\/g, "/");
}

export function isFolderUnderRoot(folderPath: string, rootPath: string): boolean {
  const folder = normalizeFolderPath(folderPath);
  const root = normalizeFolderPath(rootPath);
  if (folder === root) return true;
  return folder.startsWith(`${root}/`);
}

export function findJumpSectionPath(
  sections: GallerySectionRef[],
  targetFolderPath: string,
): string | null {
  if (sections.length === 0) return null;

  const target = normalizeFolderPath(targetFolderPath).toLowerCase();
  const exact = sections.find(
    (s) => normalizeFolderPath(s.folderPath).toLowerCase() === target,
  );
  if (exact) return exact.folderPath;

  const under = sections.find((s) => isFolderUnderRoot(s.folderPath, targetFolderPath));
  return under?.folderPath ?? null;
}

/**
 * Determines which folder section is active based on scroll position.
 */
export function findActiveFolderFromScroll(
  sections: GallerySectionRef[],
  container: HTMLElement,
  markers: Map<string, HTMLElement>,
  sectionElements: Map<string, HTMLElement>,
  anchorOffset = 72,
): string | null {
  if (sections.length === 0) return null;

  const atBottom =
    container.scrollTop + container.clientHeight >= container.scrollHeight - 16;
  if (atBottom) {
    return sections[sections.length - 1].folderPath;
  }

  const containerTop = container.getBoundingClientRect().top;
  const anchorY = containerTop + anchorOffset;

  // Prefer the section whose block contains the anchor line (works for last folder too).
  for (let i = sections.length - 1; i >= 0; i--) {
    const section = sections[i];
    const element = sectionElements.get(section.folderPath);
    if (!element) continue;

    const rect = element.getBoundingClientRect();
    if (rect.top <= anchorY + 4 && rect.bottom > anchorY - 8) {
      return section.folderPath;
    }
  }

  // Fallback: last section whose marker has crossed the anchor.
  let activeIndex = 0;
  for (let i = 0; i < sections.length; i++) {
    const marker = markers.get(sections[i].folderPath);
    if (!marker) continue;

    if (marker.getBoundingClientRect().top <= anchorY + 4) {
      activeIndex = i;
    }
  }

  return sections[activeIndex]?.folderPath ?? sections[0].folderPath;
}
