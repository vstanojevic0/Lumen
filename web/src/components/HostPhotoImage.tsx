import { useEffect, useState } from "react";
import { ImageOff } from "lucide-react";
import { mediaPreviewUrl, mediaThumbUrl } from "../lumen/mediaUrls";
import { useMediaBase } from "../lumen/useMediaBase";
import { useVisibility } from "../lumen/useVisibility";

export function HostPhotoImage({
  path,
  alt,
  className = "h-full w-full object-cover",
  maxEdge = "thumb",
  eager = false,
}: {
  path?: string;
  alt: string;
  className?: string;
  maxEdge?: "thumb" | "preview";
  /** Load immediately (inspector / single preview) instead of waiting for scroll. */
  eager?: boolean;
}) {
  const mediaBase = useMediaBase();
  const { ref, visible } = useVisibility(Boolean(path), eager);
  const [loaded, setLoaded] = useState(false);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    setLoaded(false);
    setFailed(false);
  }, [path, maxEdge, mediaBase]);

  const waitingForHost = Boolean(path) && !mediaBase;
  const canLoad = Boolean(path && visible && mediaBase && !failed);
  const src = canLoad
    ? maxEdge === "preview"
      ? mediaPreviewUrl(path!, mediaBase)
      : mediaThumbUrl(path!, mediaBase)
    : "";

  return (
    <div ref={ref} className="relative h-full w-full overflow-hidden" aria-label={alt}>
      {!loaded && !failed ? (
        <div className="absolute inset-0 animate-pulse bg-white/5" />
      ) : null}
      {failed ? (
        <div className="absolute inset-0 flex flex-col items-center justify-center bg-[linear-gradient(135deg,#172235,#0b111c)] text-white/38">
          <ImageOff size={22} />
          <span className="mt-2 max-w-[80%] truncate text-[10px]">{alt}</span>
        </div>
      ) : null}
      {waitingForHost ? (
        <div className="absolute inset-0 flex items-center justify-center text-[10px] text-white/30">
          …
        </div>
      ) : null}
      {src ? (
        <img
          key={src}
          src={src}
          alt={alt}
          className={className}
          loading={eager ? "eager" : "lazy"}
          decoding="async"
          draggable={false}
          onLoad={() => setLoaded(true)}
          onError={() => {
            setLoaded(false);
            setFailed(true);
          }}
        />
      ) : null}
    </div>
  );
}
