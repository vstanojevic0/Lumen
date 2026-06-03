import type { PhotoItem } from "../types";
import { HostPhotoImage } from "./HostPhotoImage";

interface FilmstripProps {
  photos: PhotoItem[];
  selectedId: string;
  onSelect: (id: string) => void;
}

export function Filmstrip({ photos, selectedId, onSelect }: FilmstripProps) {
  return (
    <div className="mx-8 mb-6 rounded-2xl border border-white/10 bg-[#132235]/72 p-2 shadow-xl shadow-black/25 backdrop-blur">
      <div className="flex gap-2 overflow-x-auto pb-1">
        {photos.map((photo) => {
          const selected = photo.id === selectedId;
          return (
            <button
              key={photo.id}
              type="button"
              onClick={() => onSelect(photo.id)}
              className={`relative h-[76px] w-[104px] shrink-0 overflow-hidden rounded-lg border transition ${
                selected
                  ? "border-[#087bff] ring-2 ring-[#087bff] ring-offset-2 ring-offset-[#132235]"
                  : "border-white/8 opacity-[0.76] hover:opacity-100"
              }`}
            >
              {photo.path ? (
                <HostPhotoImage
                  path={photo.path}
                  alt=""
                  className="h-full w-full object-cover"
                />
              ) : (
                <img
                  src={photo.src}
                  alt=""
                  className="h-full w-full object-cover"
                  onError={(e) => {
                    e.currentTarget.style.visibility = "hidden";
                  }}
                />
              )}
            </button>
          );
        })}
      </div>
    </div>
  );
}
