import type { EditState } from "../types";
import { defaultEditValues } from "../types";

export type PresetId =
  | "original"
  | "bright"
  | "cinematic"
  | "bw"
  | "warm"
  | "cool"
  | "lumen-boost";

export const presets: Record<
  PresetId,
  { label: string; patch: Partial<EditState> }
> = {
  original: { label: "Original", patch: defaultEditValues() },
  bright: {
    label: "Bright",
    patch: {
      exposure: 0.35,
      contrast: 12,
      shadows: 18,
      highlights: -8,
      saturation: 8,
      grayscale: false,
    },
  },
  cinematic: {
    label: "Cinematic",
    patch: {
      exposure: -0.15,
      contrast: 22,
      shadows: 12,
      highlights: -18,
      blacks: -12,
      saturation: -6,
      clarity: 15,
      temperature: 8,
      grayscale: false,
    },
  },
  bw: {
    label: "Black & White",
    patch: {
      grayscale: true,
      contrast: 18,
      clarity: 10,
      saturation: -100,
      vibrance: 0,
    },
  },
  warm: {
    label: "Warm",
    patch: {
      temperature: 42,
      tint: 6,
      exposure: 0.12,
      saturation: 12,
      vibrance: 10,
      grayscale: false,
    },
  },
  cool: {
    label: "Cool",
    patch: {
      temperature: -38,
      tint: -8,
      exposure: 0.05,
      saturation: 6,
      shadows: 8,
      grayscale: false,
    },
  },
  "lumen-boost": {
    label: "Lumen Boost",
    patch: {
      exposure: 0.28,
      contrast: 16,
      highlights: -12,
      shadows: 22,
      vibrance: 24,
      saturation: 10,
      clarity: 18,
      sharpness: 12,
      temperature: 6,
      grayscale: false,
    },
  },
};
