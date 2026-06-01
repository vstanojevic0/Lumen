import { Copy, Flag, Star } from "lucide-react";
import { buildPreviewStyle } from "../lib/filters";
import { usePreview } from "../lumen/useThumbnail";
import type { AspectRatio, EditState, PhotoItem } from "../types";
import { Filmstrip } from "./Filmstrip";

interface EditingCanvasProps {
  photo: PhotoItem;
  photos: PhotoItem[];
  edits: EditState;
  zoom: number;
  onSelectPhoto: (id: string) => void;
  onToggleFavorite: () => void;
  onToggleFlag: () => void;
  onCopyEdits: () => void;
}

function aspectClass(ratio: AspectRatio): string {
  switch (ratio) {
    case "1:1":
      return "aspect-square";
    case "4:3":
      return "aspect-[4/3]";
    case "16:9":
      return "aspect-video";
    default:
      return "";
  }
}

export function EditingCanvas({
  photo,
  photos,
  edits,
  zoom,
  onSelectPhoto,
  onToggleFavorite,
  onToggleFlag,
  onCopyEdits,
}: EditingCanvasProps) {
  const { imageStyle, warmOverlay, coolOverlay, tintOverlay } = buildPreviewStyle(edits);
  const scale = zoom / 100;
  const hostPreview = usePreview(photo.path, Boolean(photo.path));
  const displaySrc = photo.path ? hostPreview || photo.src : photo.src;
  const isLoading = Boolean(photo.path) && !displaySrc;

  return (
    <div className="flex min-h-0 min-w-0 flex-1 flex-col bg-[#060910]">
      <div className="relative flex min-h-0 flex-1 flex-col overflow-hidden">
        <div className="flex min-h-0 flex-1 items-center justify-center overflow-hidden p-4">
          <div
            className="flex max-h-full max-w-full items-center justify-center transition-transform duration-150 ease-out"
            style={{
              transform: `scale(${scale})`,
              transformOrigin: "center center",
            }}
          >
            <div
              className={`relative inline-flex max-h-full max-w-full items-center justify-center overflow-hidden rounded-lg shadow-2xl shadow-black/50 ${
                aspectClass(edits.aspectRatio)
              } ${edits.cropMode ? "ring-1 ring-white/20" : ""}`}
            >
              {isLoading ? (
                <div className="flex h-[min(60vh,480px)] w-[min(80vw,640px)] min-h-[200px] min-w-[280px] items-center justify-center rounded-lg bg-white/5">
                  <span className="text-sm text-white/40">Loading preview…</span>
                </div>
              ) : (
                <img
                  src={displaySrc}
                  alt={photo.title}
                  className="block max-h-[min(72vh,720px)] max-w-full object-contain"
                  style={imageStyle}
                  draggable={false}
                />
              )}
              {!isLoading ? (
                <>
                  <div className="pointer-events-none absolute inset-0" style={warmOverlay} />
                  <div className="pointer-events-none absolute inset-0" style={coolOverlay} />
                  <div className="pointer-events-none absolute inset-0" style={tintOverlay} />
                </>
              ) : null}

              {edits.cropMode && !isLoading ? (
                <div className="pointer-events-none absolute inset-4 border-2 border-dashed border-[#3b9bff]/80">
                  <div className="absolute inset-0 grid grid-cols-3 grid-rows-3">
                    {Array.from({ length: 9 }).map((_, i) => (
                      <div key={i} className="border border-white/15" />
                    ))}
                  </div>
                </div>
              ) : null}
            </div>
          </div>
        </div>

        <div className="absolute bottom-3 left-1/2 z-10 flex -translate-x-1/2 flex-wrap items-center justify-center gap-2 rounded-xl border border-white/10 bg-[#0a0e14]/90 px-3 py-2 shadow-lg backdrop-blur">
          <button
            type="button"
            onClick={onToggleFavorite}
            title={photo.favorite ? "Remove from favorites" : "Add to favorites"}
            className={`flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-xs transition ${
              photo.favorite
                ? "border-[#f5c842]/40 bg-[#f5c842]/15 text-[#f5c842]"
                : "border-white/10 bg-white/5 text-white/60 hover:text-white"
            }`}
          >
            <Star size={16} className={photo.favorite ? "fill-current" : ""} />
            Favorite
          </button>

          <button
            type="button"
            onClick={onToggleFlag}
            className={`flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-xs transition ${
              photo.flagged
                ? "border-amber-400/40 bg-amber-500/15 text-amber-200"
                : "border-white/10 bg-white/5 text-white/60 hover:text-white"
            }`}
          >
            <Flag size={14} />
            Flag
          </button>

          <button
            type="button"
            onClick={onCopyEdits}
            className="flex items-center gap-1.5 rounded-lg border border-white/10 bg-white/5 px-3 py-1.5 text-xs text-white/70 hover:bg-white/10 hover:text-white"
          >
            <Copy size={14} />
            Copy Edits
          </button>
        </div>
      </div>

      <Filmstrip photos={photos} selectedId={photo.id} onSelect={onSelectPhoto} />
    </div>
  );
}
