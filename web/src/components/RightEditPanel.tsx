import { RotateCcw } from "lucide-react";
import { presets, type PresetId } from "../lib/presets";
import type { AspectRatio, EditState, PhotoItem } from "../types";
import { CollapsibleSection } from "./CollapsibleSection";
import { PresetButton } from "./PresetButton";
import { SliderControl } from "./SliderControl";

interface RightEditPanelProps {
  photo: PhotoItem;
  edits: EditState;
  activePreset: PresetId | null;
  onChange: <K extends keyof EditState>(key: K, value: EditState[K]) => void;
  onApplyPreset: (id: PresetId) => void;
  onReset: () => void;
  onCopyEdits: () => void;
  onExport: () => void;
}

function HistogramGraphic() {
  const bars = [
    12, 28, 45, 62, 78, 88, 95, 100, 92, 85, 72, 58, 42, 30, 22, 18, 14, 10, 8, 6,
    5, 8, 14, 22, 35, 48, 55, 50, 40, 32, 24, 18, 14, 10, 8, 6, 5, 4, 3, 2,
  ];
  return (
    <div className="relative h-16 overflow-hidden rounded-lg bg-black/30">
      <svg viewBox="0 0 200 64" className="h-full w-full" preserveAspectRatio="none">
        <defs>
          <linearGradient id="hg" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="#3b9bff" stopOpacity="0.9" />
            <stop offset="100%" stopColor="#3b9bff" stopOpacity="0.1" />
          </linearGradient>
        </defs>
        {bars.map((h, i) => (
          <rect
            key={i}
            x={i * 5}
            y={64 - (h * 64) / 100}
            width="4"
            height={(h * 64) / 100}
            fill="url(#hg)"
            opacity={0.85}
          />
        ))}
      </svg>
    </div>
  );
}

function ToneCurveCard() {
  return (
    <div className="relative h-28 overflow-hidden rounded-lg border border-white/8 bg-[#0a1018]">
      <svg viewBox="0 0 120 80" className="h-full w-full p-2">
        <line x1="8" y1="72" x2="112" y2="8" stroke="white" strokeOpacity="0.08" />
        <path
          d="M 8 72 Q 40 60 70 35 T 112 12"
          fill="none"
          stroke="#3b9bff"
          strokeWidth="2"
        />
        <circle cx="8" cy="72" r="3" fill="#3b9bff" />
        <circle cx="70" cy="35" r="3" fill="#7eb8ff" />
        <circle cx="112" cy="12" r="3" fill="#3b9bff" />
      </svg>
    </div>
  );
}

export function RightEditPanel({
  photo,
  edits,
  activePreset,
  onChange,
  onApplyPreset,
  onReset,
  onCopyEdits,
  onExport,
}: RightEditPanelProps) {
  const ratios: AspectRatio[] = ["free", "1:1", "4:3", "16:9"];

  return (
    <aside className="glass flex h-full w-[300px] shrink-0 flex-col border-r border-white/8">
      <div className="border-b border-white/8 px-4 py-3">
        <div className="truncate text-sm font-medium text-white/90">{photo.title}</div>
        <div className="mt-0.5 truncate text-[11px] text-white/40">Edit &amp; develop</div>
      </div>
      <div className="flex-1 overflow-y-auto p-3 space-y-2.5">
        <CollapsibleSection title="Histogram" defaultOpen>
          <HistogramGraphic />
          <div className="grid grid-cols-2 gap-2 text-[11px] text-white/55">
            <Meta label="ISO" value={photo.iso > 0 ? String(photo.iso) : "—"} />
            <Meta label="Lens" value={photo.focalLength} />
            <Meta label="Aperture" value={photo.aperture} />
            <Meta label="Shutter" value={photo.shutter} />
          </div>
        </CollapsibleSection>

        <CollapsibleSection title="Light">
          <SliderControl
            label="Exposure"
            value={edits.exposure}
            min={-2}
            max={2}
            step={0.01}
            onChange={(v) => onChange("exposure", v)}
          />
          <SliderControl
            label="Contrast"
            value={edits.contrast}
            min={-100}
            max={100}
            onChange={(v) => onChange("contrast", v)}
          />
          <SliderControl
            label="Highlights"
            value={edits.highlights}
            min={-100}
            max={100}
            onChange={(v) => onChange("highlights", v)}
          />
          <SliderControl
            label="Shadows"
            value={edits.shadows}
            min={-100}
            max={100}
            onChange={(v) => onChange("shadows", v)}
          />
          <SliderControl
            label="Whites"
            value={edits.whites}
            min={-100}
            max={100}
            onChange={(v) => onChange("whites", v)}
          />
          <SliderControl
            label="Blacks"
            value={edits.blacks}
            min={-100}
            max={100}
            onChange={(v) => onChange("blacks", v)}
          />
        </CollapsibleSection>

        <CollapsibleSection title="Color">
          <SliderControl
            label="Temperature"
            value={edits.temperature}
            min={-100}
            max={100}
            onChange={(v) => onChange("temperature", v)}
          />
          <SliderControl
            label="Tint"
            value={edits.tint}
            min={-100}
            max={100}
            onChange={(v) => onChange("tint", v)}
          />
          <SliderControl
            label="Vibrance"
            value={edits.vibrance}
            min={-100}
            max={100}
            onChange={(v) => onChange("vibrance", v)}
          />
          <SliderControl
            label="Saturation"
            value={edits.saturation}
            min={-100}
            max={100}
            onChange={(v) => onChange("saturation", v)}
          />
        </CollapsibleSection>

        <CollapsibleSection title="Tone Curve" defaultOpen={false}>
          <ToneCurveCard />
        </CollapsibleSection>

        <CollapsibleSection title="Detail" defaultOpen={false}>
          <SliderControl
            label="Sharpness"
            value={edits.sharpness}
            min={0}
            max={100}
            onChange={(v) => onChange("sharpness", v)}
          />
          <SliderControl
            label="Clarity"
            value={edits.clarity}
            min={-100}
            max={100}
            onChange={(v) => onChange("clarity", v)}
          />
          <SliderControl
            label="Noise Reduction"
            value={edits.noiseReduction}
            min={0}
            max={100}
            onChange={(v) => onChange("noiseReduction", v)}
          />
        </CollapsibleSection>

        <CollapsibleSection title="Crop & Straighten">
          <button
            type="button"
            onClick={() => onChange("cropMode", !edits.cropMode)}
            className={`w-full rounded-lg border py-2 text-xs font-medium transition ${
              edits.cropMode
                ? "border-[#3b9bff] bg-[#3b9bff]/20 text-white"
                : "border-white/10 bg-white/5 text-white/70 hover:bg-white/10"
            }`}
          >
            {edits.cropMode ? "Crop overlay on" : "Toggle crop overlay"}
          </button>
          <div className="flex flex-wrap gap-1.5">
            {ratios.map((r) => (
              <button
                key={r}
                type="button"
                onClick={() => onChange("aspectRatio", r)}
                className={`rounded-md border px-2 py-1 text-[10px] ${
                  edits.aspectRatio === r
                    ? "border-[#3b9bff] bg-[#3b9bff]/20 text-white"
                    : "border-white/10 text-white/55 hover:bg-white/8"
                }`}
              >
                {r === "free" ? "Free" : r}
              </button>
            ))}
          </div>
          <SliderControl
            label="Rotation"
            value={edits.rotation}
            min={-45}
            max={45}
            step={0.5}
            onChange={(v) => onChange("rotation", v)}
            format={(v) => `${v.toFixed(1)}°`}
          />
          <button
            type="button"
            onClick={() => onChange("rotation", 0)}
            className="w-full rounded-lg border border-white/10 bg-white/5 py-2 text-xs text-white/70 hover:bg-white/10"
          >
            Straighten
          </button>
        </CollapsibleSection>

        <CollapsibleSection title="Presets">
          <div className="grid grid-cols-2 gap-1.5">
            {(Object.keys(presets) as PresetId[]).map((id) => (
              <PresetButton
                key={id}
                label={presets[id].label}
                active={activePreset === id}
                onClick={() => onApplyPreset(id)}
              />
            ))}
          </div>
        </CollapsibleSection>

        <CollapsibleSection title="Actions" defaultOpen>
          <button
            type="button"
            onClick={onReset}
            className="flex w-full items-center justify-center gap-2 rounded-lg border border-white/10 bg-white/5 py-2.5 text-sm text-white/75 hover:bg-white/10"
          >
            <RotateCcw size={15} />
            Reset edits
          </button>
          <button
            type="button"
            onClick={onCopyEdits}
            className="w-full rounded-lg border border-white/10 bg-white/5 py-2.5 text-sm text-white/75 hover:bg-white/10"
          >
            Copy edits
          </button>
          <button
            type="button"
            onClick={onExport}
            className="w-full rounded-xl bg-[#3b9bff] py-2.5 text-sm font-medium text-white shadow-lg shadow-[#3b9bff]/20 hover:bg-[#4aa5ff]"
          >
            Export…
          </button>
        </CollapsibleSection>
      </div>
    </aside>
  );
}

function Meta({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md bg-white/5 px-2 py-1.5">
      <div className="text-white/35">{label}</div>
      <div className="font-medium text-white/80">{value}</div>
    </div>
  );
}
