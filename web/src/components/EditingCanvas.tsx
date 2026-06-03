import { useEffect, useState } from "react";
import { Copy, Flag, Star } from "lucide-react";
import { buildPreviewStyle } from "../lib/filters";
import { mediaPreviewUrl } from "../lumen/mediaUrls";
import { useMediaBase } from "../lumen/useMediaBase";
import type { AspectRatio, EditState, PhotoItem } from "../types";
import { Filmstrip } from "./Filmstrip";

interface EditingCanvasProps {
  photo: PhotoItem;
  filmstripPhotos: PhotoItem[];
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
  filmstripPhotos,
  edits,
  zoom,
  onSelectPhoto,
  onToggleFavorite,
  onToggleFlag,
  onCopyEdits,
}: EditingCanvasProps) {
  const { imageStyle, warmOverlay, coolOverlay, tintOverlay } = buildPreviewStyle(edits);
  const scale = zoom / 100;
  const mediaBase = useMediaBase();
  const displaySrc =
    photo.path && mediaBase ? mediaPreviewUrl(photo.path, mediaBase) : photo.src;
  const waitingForHost = Boolean(photo.path) && !mediaBase;
  const [previewReady, setPreviewReady] = useState(false);
  const [previewFailed, setPreviewFailed] = useState(false);

  useEffect(() => {
    setPreviewReady(false);
    setPreviewFailed(false);
  }, [photo.path, photo.src, mediaBase]);

  useEffect(() => {
    if (!displaySrc || previewReady || previewFailed) return;
    const t = window.setTimeout(() => setPreviewFailed(true), 15000);
    return () => window.clearTimeout(t);
  }, [displaySrc, previewFailed, previewReady]);

  const isLoading =
    waitingForHost || (Boolean(displaySrc) && !previewReady && !previewFailed);

  return (
    <div className="flex min-h-0 min-w-0 flex-1 flex-col bg-transparent">
      <div className="relative flex min-h-0 flex-1 flex-col overflow-hidden">
        <div className="flex min-h-0 flex-1 items-center justify-center overflow-hidden px-9 py-7">
          <div
            className="flex max-h-full max-w-full items-center justify-center transition-transform duration-150 ease-out"
            style={{
              transform: `scale(${scale})`,
              transformOrigin: "center center",
            }}
          >
            <div
              className={`relative inline-flex max-h-full max-w-full items-center justify-center overflow-hidden rounded-sm border border-white/18 bg-black/25 shadow-2xl shadow-black/55 ${
                aspectClass(edits.aspectRatio)
              } ${edits.cropMode ? "ring-1 ring-white/20" : ""}`}
            >
              <div className="relative flex min-h-[200px] min-w-[280px] items-center justify-center">
                {previewFailed ? (
                  <div className="flex h-[min(60vh,480px)] w-[min(80vw,640px)] flex-col items-center justify-center rounded-lg bg-white/5 px-8 text-center">
                    <span className="text-sm font-medium text-white/60">Preview unavailable</span>
                    <span className="mt-1 text-xs text-white/35">{photo.title}</span>
                  </div>
                ) : null}

                {isLoading ? (
                  <div className="absolute inset-0 z-10 flex items-center justify-center rounded-lg bg-[#060910]/90">
                    <span className="text-sm text-white/40">
                      {waitingForHost ? "Connecting to desktop…" : "Loading preview…"}
                    </span>
                  </div>
                ) : null}

                {displaySrc && !previewFailed ? (
                  <img
                    key={displaySrc}
                    src={displaySrc}
                    alt={photo.title}
                    className={`block max-h-[min(72vh,720px)] max-w-full object-contain transition-opacity duration-200 ${
                      previewReady ? "opacity-100" : "opacity-0"
                    }`}
                    style={imageStyle}
                    draggable={false}
                    decoding="async"
                    onLoad={() => setPreviewReady(true)}
                    onError={() => {
                      setPreviewReady(false);
                      setPreviewFailed(true);
                    }}
                  />
                ) : null}

                {!displaySrc && !previewFailed && !isLoading ? (
                  <div className="flex h-[min(40vh,320px)] w-[min(60vw,480px)] items-center justify-center rounded-lg bg-white/5 text-sm text-white/40">
                    No preview source
                  </div>
                ) : null}
              </div>
              {previewReady ? (
                <>
                  <div className="pointer-events-none absolute inset-0" style={warmOverlay} />
                  <div className="pointer-events-none absolute inset-0" style={coolOverlay} />
                  <div className="pointer-events-none absolute inset-0" style={tintOverlay} />
                </>
              ) : null}

              {edits.cropMode && previewReady ? (
                <div className="pointer-events-none absolute inset-4 border-2 border-dashed border-[#75c9a3]/85">
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

        <div className="absolute bottom-4 left-1/2 z-10 flex -translate-x-1/2 flex-wrap items-center justify-center gap-2 rounded-2xl border border-white/10 bg-[#132235]/82 px-3 py-2 shadow-lg shadow-black/30 backdrop-blur">
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

      <Filmstrip
        photos={filmstripPhotos}
        selectedId={photo.id}
        onSelect={onSelectPhoto}
      />
    </div>
  );
}
