import { useEffect, useRef, useState } from "react";
import { getMediaBase, mediaPreviewUrl, mediaThumbUrl } from "../lumen/mediaUrls";

export function HostPhotoImage({
  path,
  alt,
  className = "h-full w-full object-cover",
  maxEdge = "thumb",
}: {
  path?: string;
  alt: string;
  className?: string;
  maxEdge?: "thumb" | "preview";
}) {
  const ref = useRef<HTMLDivElement>(null);
  const [visible, setVisible] = useState(false);
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    setLoaded(false);
  }, [path, maxEdge]);

  useEffect(() => {
    const el = ref.current;
    if (!el || !path) return;

    const observer = new IntersectionObserver(
      ([entry]) => setVisible(entry.isIntersecting),
      { rootMargin: "120px" },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [path]);

  const canLoad = Boolean(path && visible && getMediaBase());
  const src = canLoad
    ? maxEdge === "preview"
      ? mediaPreviewUrl(path!)
      : mediaThumbUrl(path!)
    : "";

  return (
    <div ref={ref} className="relative h-full w-full overflow-hidden" aria-label={alt}>
      {!loaded ? <div className="absolute inset-0 animate-pulse bg-white/5" /> : null}
      {src ? (
        <img
          src={src}
          alt={alt}
          className={className}
          loading="lazy"
          decoding="async"
          draggable={false}
          onLoad={() => setLoaded(true)}
          onError={() => setLoaded(false)}
        />
      ) : null}
    </div>
  );
}
