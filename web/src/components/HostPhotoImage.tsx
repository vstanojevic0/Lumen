import { useThumbnail } from "../lumen/useThumbnail";

export function HostPhotoImage({
  path,
  alt,
  className,
  maxEdge = "thumb",
}: {
  path?: string;
  alt: string;
  className?: string;
  maxEdge?: "thumb" | "preview";
}) {
  const thumb = useThumbnail(path, maxEdge === "thumb");
  const src = thumb || undefined;

  if (!src) {
    return (
      <div
        className={`animate-pulse bg-white/5 ${className ?? ""}`}
        aria-label={alt}
      />
    );
  }

  return <img src={src} alt={alt} className={className} />;
}
