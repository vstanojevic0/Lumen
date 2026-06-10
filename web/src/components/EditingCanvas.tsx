import type { MouseEvent } from "react";
import { useCallback, useEffect, useState } from "react";
import { defaultCropForAspect, fitCropToAspect } from "../lib/crop";
import { buildPreviewStyle } from "../lib/filters";
import { rotateOrientation } from "../lib/rotation";
import { mediaFullUrl, mediaPreviewUrl } from "../lumen/mediaUrls";
import { useMediaBase } from "../lumen/useMediaBase";
import type { AspectRatio, EditState, PhotoItem } from "../types";
import { applyCropClipStyle, CropOverlay } from "./CropOverlay";
import { Filmstrip } from "./Filmstrip";
import { PreviewToolBar } from "./PreviewToolBar";

interface EditingCanvasProps {
  photo: PhotoItem;
  filmstripPhotos: PhotoItem[];
  edits: EditState;
  zoom: number;
  onSelectPhoto: (id: string) => void;
  onPhotoContextMenu?: (photoId: string, event: MouseEvent) => void;
  onChange: <K extends keyof EditState>(key: K, value: EditState[K]) => void;
  onPatch: (partial: Partial<EditState>) => void;
}

export function EditingCanvas({
  photo,
  filmstripPhotos,
  edits,
  zoom,
  onSelectPhoto,
  onPhotoContextMenu,
  onChange,
  onPatch,
}: EditingCanvasProps) {
  const { imageStyle, frameStyle, warmOverlay, coolOverlay, tintOverlay } = buildPreviewStyle(edits);
  const scale = zoom / 100;
  const mediaBase = useMediaBase();
  const previewSrc =
    photo.path && mediaBase ? mediaPreviewUrl(photo.path, mediaBase) : photo.src;
  const fullSrc =
    photo.path && mediaBase ? mediaFullUrl(photo.path, mediaBase) : photo.src;
  const [displaySrc, setDisplaySrc] = useState(previewSrc || fullSrc);
  const waitingForHost = Boolean(photo.path) && !mediaBase;
  const [previewReady, setPreviewReady] = useState(false);
  const [previewFailed, setPreviewFailed] = useState(false);

  useEffect(() => {
    setPreviewReady(false);
    setPreviewFailed(false);
    setDisplaySrc(previewSrc || fullSrc);
  }, [photo.path, photo.src, mediaBase, previewSrc, fullSrc]);

  useEffect(() => {
    if (!fullSrc || fullSrc === previewSrc || !previewReady) return;

    const loader = new Image();
    loader.decoding = "async";
    loader.onload = () => setDisplaySrc(fullSrc);
    loader.onerror = () => {};
    loader.src = fullSrc;
    return () => {
      loader.onload = null;
      loader.onerror = null;
    };
  }, [fullSrc, previewSrc, previewReady]);

  useEffect(() => {
    if (!displaySrc || previewReady || previewFailed) return;
    const t = window.setTimeout(() => setPreviewFailed(true), 45000);
    return () => window.clearTimeout(t);
  }, [displaySrc, previewFailed, previewReady]);

  const handleRotateLeft = useCallback(() => {
    onChange("orientation", rotateOrientation(edits.orientation, -90));
  }, [edits.orientation, onChange]);

  const handleRotateRight = useCallback(() => {
    onChange("orientation", rotateOrientation(edits.orientation, 90));
  }, [edits.orientation, onChange]);

  const handleToggleCrop = useCallback(() => {
    const next = !edits.cropMode;
    onPatch({
      cropMode: next,
      cropRect: next ? defaultCropForAspect(edits.aspectRatio) : edits.cropRect,
    });
  }, [edits.aspectRatio, edits.cropMode, edits.cropRect, onPatch]);

  const handleAspectRatio = useCallback(
    (ratio: AspectRatio) => {
      onPatch({
        aspectRatio: ratio,
        cropRect: edits.cropMode ? fitCropToAspect(edits.cropRect, ratio) : edits.cropRect,
      });
    },
    [edits.cropMode, edits.cropRect, onPatch],
  );

  const isLoading =
    waitingForHost || (Boolean(displaySrc) && !previewReady && !previewFailed);

  const cropClip = applyCropClipStyle(edits.cropRect, edits.cropMode);

  return (
    <div className="flex min-h-0 min-w-0 flex-1 flex-col bg-transparent">
      <div className="relative flex min-h-0 flex-1 flex-col overflow-hidden">
        <div
          className="flex min-h-0 flex-1 items-center justify-center overflow-visible px-6 py-5"
          data-photo-viewer
        >
          <div
            className="flex max-h-full max-w-full items-center justify-center transition-transform duration-150 ease-out"
            style={{
              transform: `scale(${scale})`,
              transformOrigin: "center center",
            }}
          >
            <div className="flex items-center justify-center" style={frameStyle}>
              <div className="relative inline-flex max-h-full max-w-full items-center justify-center overflow-visible rounded-sm border border-white/18 bg-black/25 shadow-2xl shadow-black/55">
                <div className="relative flex min-h-[200px] min-w-[280px] max-h-[calc(100vh-10rem)] max-w-[min(100%,calc(100vw-12rem))] items-center justify-center">
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
                      onContextMenu={(event) => {
                        event.preventDefault();
                        onPhotoContextMenu?.(photo.id, event);
                      }}
                      className={`block max-h-[calc(100vh-10rem)] max-w-[min(100%,calc(100vw-12rem))] object-contain transition-opacity duration-200 ${
                        previewReady ? "opacity-100" : "opacity-0"
                      }`}
                      style={{ ...imageStyle, ...cropClip }}
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

                  {previewReady && !edits.cropMode ? (
                    <>
                      <div className="pointer-events-none absolute inset-0" style={warmOverlay} />
                      <div className="pointer-events-none absolute inset-0" style={coolOverlay} />
                      <div className="pointer-events-none absolute inset-0" style={tintOverlay} />
                    </>
                  ) : null}

                  {edits.cropMode && previewReady ? (
                    <CropOverlay
                      cropRect={edits.cropRect}
                      aspectRatio={edits.aspectRatio}
                      onChange={(rect) => onChange("cropRect", rect)}
                    />
                  ) : null}
                </div>
              </div>
            </div>
          </div>
        </div>

        {previewReady ? (
          <PreviewToolBar
            cropMode={edits.cropMode}
            aspectRatio={edits.aspectRatio}
            onRotateLeft={handleRotateLeft}
            onRotateRight={handleRotateRight}
            onToggleCrop={handleToggleCrop}
            onAspectRatio={handleAspectRatio}
          />
        ) : null}
      </div>

      <Filmstrip
        photos={filmstripPhotos}
        selectedId={photo.id}
        onSelect={onSelectPhoto}
        onPhotoContextMenu={onPhotoContextMenu}
      />
    </div>
  );
}
