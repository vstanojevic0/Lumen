export type AspectRatio = "free" | "1:1" | "4:3" | "16:9";
export type AppMode = "library" | "edit";
export type PhotoOrientation = 0 | 90 | 180 | 270;

export interface EditValues {
  exposure: number;
  contrast: number;
  highlights: number;
  shadows: number;
  whites: number;
  blacks: number;
  temperature: number;
  tint: number;
  vibrance: number;
  saturation: number;
  sharpness: number;
  clarity: number;
  noiseReduction: number;
  rotation: number;
}

export interface PhotoItem {
  id: string;
  title: string;
  /** Display URL (thumbnail/preview); filled by host or demo data. */
  src: string;
  /** Absolute path when running inside Lumen desktop host. */
  path?: string;
  iso: number;
  focalLength: string;
  aperture: string;
  shutter: string;
  favorite: boolean;
  flagged: boolean;
}

export interface EditState extends EditValues {
  orientation: PhotoOrientation;
  cropMode: boolean;
  aspectRatio: AspectRatio;
  grayscale: boolean;
}

export const defaultEditValues = (): EditState => ({
  exposure: 0,
  contrast: 0,
  highlights: 0,
  shadows: 0,
  whites: 0,
  blacks: 0,
  temperature: 0,
  tint: 0,
  vibrance: 0,
  saturation: 0,
  sharpness: 0,
  clarity: 0,
  noiseReduction: 0,
  rotation: 0,
  orientation: 0,
  cropMode: false,
  aspectRatio: "free",
  grayscale: false,
});
