import type { PhotoItem } from "../types";
import { HostPhotoImage } from "./HostPhotoImage";

interface FilmstripProps {
  photos: PhotoItem[];
  selectedId: string;
  onSelect: (id: string) => void;
}

export function Filmstrip({ photos, selectedId, onSelect }: FilmstripProps) {
  return (
    <div className="glass mx-4 mb-4 rounded-xl border border-white/8 p-2">
      <div className="flex gap-2 overflow-x-auto pb-1">
        {photos.map((photo) => {
          const selected = photo.id === selectedId;
          return (
            <button
              key={photo.id}
              type="button"
              onClick={() => onSelect(photo.id)}
              className={`relative h-[72px] w-[96px] shrink-0 overflow-hidden rounded-lg transition ${
                selected
                  ? "ring-2 ring-[#3b9bff] ring-offset-2 ring-offset-[#0a0e14]"
                  : "opacity-70 hover:opacity-100"
              }`}
            >
              {photo.path ? (
                <HostPhotoImage
                  path={photo.path}
                  alt=""
                  className="h-full w-full object-cover"
                />
              ) : (
                <img src={photo.src} alt="" className="h-full w-full object-cover" />
              )}
            </button>
          );
        })}
      </div>
    </div>
  );
}
