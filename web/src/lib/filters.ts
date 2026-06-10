import type { CSSProperties } from "react";
import { totalRotationDegrees } from "./rotation";
import type { EditState } from "../types";

/** Maps edit sliders to CSS filter + overlay styles for live preview. */
export function buildPreviewStyle(edits: EditState): {
  imageStyle: CSSProperties;
  frameStyle: CSSProperties;
  warmOverlay: CSSProperties;
  coolOverlay: CSSProperties;
  tintOverlay: CSSProperties;
} {
  const brightness =
    1 +
    edits.exposure * 0.22 +
    edits.shadows * 0.002 -
    edits.highlights * 0.0015 +
    edits.whites * 0.001 -
    edits.blacks * 0.0015;

  const contrast = 1 + edits.contrast / 100 + edits.clarity * 0.002;
  const saturate = 1 + (edits.saturation + edits.vibrance * 0.65) / 100;

  const filters = [
    edits.grayscale ? "grayscale(1)" : null,
    `brightness(${brightness.toFixed(3)})`,
    `contrast(${contrast.toFixed(3)})`,
    `saturate(${saturate.toFixed(3)})`,
    edits.sharpness > 0 ? `contrast(${1 + edits.sharpness / 400})` : null,
    edits.noiseReduction > 0 ? `blur(${edits.noiseReduction * 0.003}px)` : null,
    `hue-rotate(${edits.tint * 0.35}deg)`,
  ]
    .filter(Boolean)
    .join(" ");

  const warmOpacity = Math.max(0, edits.temperature) / 220;
  const coolOpacity = Math.max(0, -edits.temperature) / 220;

  return {
    imageStyle: {
      filter: filters,
      transition: "filter 80ms ease",
    },
    frameStyle: {
      transform: `rotate(${totalRotationDegrees(edits.orientation, edits.rotation)}deg)`,
      transition: "transform 120ms ease",
    },
    warmOverlay: {
      background: "linear-gradient(135deg, rgba(255,180,80,0.35), transparent)",
      opacity: warmOpacity,
    },
    coolOverlay: {
      background: "linear-gradient(135deg, rgba(80,160,255,0.35), transparent)",
      opacity: coolOpacity,
    },
    tintOverlay: {
      background:
        edits.tint >= 0
          ? "rgba(180, 120, 255, 0.2)"
          : "rgba(80, 200, 140, 0.2)",
      opacity: Math.abs(edits.tint) / 180,
    },
  };
}
