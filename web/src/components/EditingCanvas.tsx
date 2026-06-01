import { Copy, Flag, Heart, Star } from "lucide-react";
import { buildPreviewStyle } from "../lib/filters";
import type { AspectRatio, EditState, PhotoItem } from "../types";
import { Filmstrip } from "./Filmstrip";

interface EditingCanvasProps {
  photo: PhotoItem;
  photos: PhotoItem[];
  edits: EditState;
  zoom: number;
  onSelectPhoto: (id: string) => void;
  onRating: (rating: number) => void;
  onToggleFavorite: () => void;
  onToggleFlag: () => void;
  onCopyEdits: () => void;
}

function aspectClass(ratio: AspectRatio): string {
  switch (ratio) {
    case "1:1":
      return "aspect-square max-h-[min(62vh,520px)]";
    case "4:3":
      return "aspect-[4/3] max-h-[min(62vh,520px)]";
    case "16:9":
      return "aspect-video max-h-[min(62vh,520px)]";
    default:
      return "max-h-[min(68vh,580px)]";
  }
}

export function EditingCanvas({
  photo,
  photos,
  edits,
  zoom,
  onSelectPhoto,
  onRating,
  onToggleFavorite,
  onToggleFlag,
  onCopyEdits,
}: EditingCanvasProps) {
  const { imageStyle, warmOverlay, coolOverlay, tintOverlay } = buildPreviewStyle(edits);
  const scale = zoom / 100;

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <div className="relative flex flex-1 flex-col items-center justify-center overflow-hidden bg-[#060910] px-6 py-4">
        <div
          className="relative flex items-center justify-center transition-transform duration-150"
          style={{ transform: `scale(${scale})` }}
        >
          <div
            className={`relative overflow-hidden rounded-lg shadow-2xl shadow-black/50 ${aspectClass(edits.aspectRatio)} ${
              edits.cropMode ? "ring-1 ring-white/20" : ""
            }`}
          >
            <img
              src={photo.src}
              alt={photo.title}
              className="max-h-[68vh] w-auto max-w-full object-contain"
              style={imageStyle}
            />
            <div className="pointer-events-none absolute inset-0" style={warmOverlay} />
            <div className="pointer-events-none absolute inset-0" style={coolOverlay} />
            <div className="pointer-events-none absolute inset-0" style={tintOverlay} />

            {edits.cropMode ? (
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

        <div className="mt-4 flex flex-wrap items-center justify-center gap-3">
          <div className="flex items-center gap-1">
            {[1, 2, 3, 4, 5].map((n) => (
              <button
                key={n}
                type="button"
                onClick={() => onRating(n)}
                className="rounded p-1 text-white/40 hover:text-[#f5c842]"
              >
                <Star
                  size={18}
                  className={n <= photo.rating ? "fill-[#f5c842] text-[#f5c842]" : ""}
                />
              </button>
            ))}
          </div>

          <button
            type="button"
            onClick={onToggleFavorite}
            className={`flex items-center gap-1.5 rounded-lg border px-3 py-1.5 text-xs transition ${
              photo.favorite
                ? "border-red-400/40 bg-red-500/15 text-red-300"
                : "border-white/10 bg-white/5 text-white/60 hover:text-white"
            }`}
          >
            <Heart size={14} className={photo.favorite ? "fill-current" : ""} />
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
