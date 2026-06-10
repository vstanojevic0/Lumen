import type { ReactNode } from "react";
import {
  Calendar,
  Camera,
  Download,
  FileImage,
  FolderOpen,
  HardDrive,
  Info,
  RotateCcw,
  RotateCw,
  SlidersHorizontal,
  X,
} from "lucide-react";
import { presets, type PresetId } from "../lib/presets";
import { rotateOrientation } from "../lib/rotation";
import { usePhotoDetails } from "../hooks/usePhotoDetails";
import type { AspectRatio, EditState, PhotoItem } from "../types";
import { CollapsibleSection } from "./CollapsibleSection";
import { PresetButton } from "./PresetButton";
import { SliderControl } from "./SliderControl";

export type InspectorTab = "info" | "edit";

interface RightEditPanelProps {
  photo: PhotoItem;
  edits: EditState;
  activePreset: PresetId | null;
  tab: InspectorTab;
  onTabChange: (tab: InspectorTab) => void;
  onClose: () => void;
  onChange: <K extends keyof EditState>(key: K, value: EditState[K]) => void;
  onApplyPreset: (id: PresetId) => void;
  onReset: () => void;
  onExport: () => void;
}

export function RightEditPanel({
  photo,
  edits,
  activePreset,
  tab,
  onTabChange,
  onClose,
  onChange,
  onApplyPreset,
  onReset,
  onExport,
}: RightEditPanelProps) {
  const ratios: AspectRatio[] = ["free", "1:1", "4:3", "16:9"];

  return (
    <aside className="glass flex h-full w-[320px] shrink-0 flex-col border-l border-white/8">
      <header className="flex shrink-0 items-center gap-2 border-b border-white/6 px-3 py-2.5">
        <button
          type="button"
          onClick={onClose}
          className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-white/50 transition hover:bg-white/8 hover:text-white"
          title="Hide panel"
        >
          <X size={17} />
        </button>

        <div className="flex min-w-0 flex-1 rounded-lg bg-white/[0.06] p-0.5">
          <TabButton
            active={tab === "info"}
            onClick={() => onTabChange("info")}
            icon={<Info size={13} />}
            label="Info"
          />
          <TabButton
            active={tab === "edit"}
            onClick={() => onTabChange("edit")}
            icon={<SlidersHorizontal size={13} />}
            label="Edit"
          />
        </div>
      </header>

      <div className="min-w-0 shrink-0 border-b border-white/6 px-4 py-3">
        <div className="truncate text-sm font-semibold text-white/92">{photo.title}</div>
        <div className="mt-0.5 truncate text-[11px] text-white/40">
          {tab === "info" ? "File metadata" : "Develop adjustments"}
        </div>
      </div>

      <div className="min-h-0 flex-1 overflow-y-auto px-3 py-3" data-photo-scroll-container>
        {tab === "info" ? (
          <PhotoInfoView photo={photo} />
        ) : (
          <EditControlsView
            edits={edits}
            activePreset={activePreset}
            ratios={ratios}
            onChange={onChange}
            onApplyPreset={onApplyPreset}
            onReset={onReset}
            onExport={onExport}
          />
        )}
      </div>
    </aside>
  );
}

function TabButton({
  active,
  onClick,
  icon,
  label,
}: {
  active: boolean;
  onClick: () => void;
  icon: ReactNode;
  label: string;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex flex-1 items-center justify-center gap-1.5 rounded-md py-1.5 text-xs font-medium transition ${
        active
          ? "bg-white/12 text-white shadow-sm"
          : "text-white/50 hover:text-white/75"
      }`}
    >
      {icon}
      {label}
    </button>
  );
}

function PhotoInfoView({ photo }: { photo: PhotoItem }) {
  const { details, loading, error } = usePhotoDetails(photo.path);

  if (loading) {
    return (
      <div className="flex h-32 items-center justify-center text-sm text-white/40">
        Loading info…
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-xl border border-red-400/20 bg-red-500/10 px-3 py-4 text-sm text-red-200/80">
        {error}
      </div>
    );
  }

  const dimensions =
    details?.width && details?.height
      ? `${details.width.toLocaleString()} × ${details.height.toLocaleString()} px`
      : "—";

  const megapixels =
    details?.width && details?.height
      ? `${((details.width * details.height) / 1_000_000).toFixed(1)} MP`
      : null;

  return (
    <div className="space-y-3">
      <InfoSection title="File">
        <InfoRow icon={FileImage} label="Name" value={details?.fileName ?? photo.title} />
        <InfoRow
          icon={HardDrive}
          label="Size"
          value={details?.fileSizeLabel ?? "—"}
        />
        <InfoRow
          icon={FileImage}
          label="Format"
          value={details?.extension?.toUpperCase() ?? "—"}
        />
      </InfoSection>

      <InfoSection title="Image">
        <InfoRow icon={Camera} label="Dimensions" value={dimensions} />
        {megapixels ? (
          <div className="px-1 text-[11px] text-white/35">{megapixels}</div>
        ) : null}
        <InfoRow
          icon={Camera}
          label="Camera"
          value={details?.cameraModel ?? "—"}
        />
      </InfoSection>

      <InfoSection title="Dates">
        <InfoRow
          icon={Calendar}
          label="Captured"
          value={formatDate(details?.dateTaken)}
        />
        <InfoRow
          icon={Calendar}
          label="Created"
          value={formatDate(details?.dateCreated)}
        />
        <InfoRow
          icon={Calendar}
          label="Modified"
          value={formatDate(details?.dateModified)}
        />
      </InfoSection>

      <InfoSection title="Location">
        <InfoRow
          icon={FolderOpen}
          label="Folder"
          value={leafFolder(details?.folderPath ?? photo.path ?? "—")}
          mono
        />
      </InfoSection>
    </div>
  );
}

function InfoSection({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-xl border border-white/6 bg-white/[0.03] p-3">
      <h3 className="mb-2.5 text-[10px] font-semibold uppercase tracking-[0.12em] text-white/35">
        {title}
      </h3>
      <div className="space-y-2">{children}</div>
    </section>
  );
}

function InfoRow({
  icon: Icon,
  label,
  value,
  mono = false,
}: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  label: string;
  value: string;
  mono?: boolean;
}) {
  return (
    <div className="flex gap-2.5">
      <Icon size={14} className="mt-0.5 shrink-0 text-white/30" />
      <div className="min-w-0 flex-1">
        <div className="text-[10px] text-white/35">{label}</div>
        <div
          className={`break-words text-[13px] leading-snug text-white/82 ${
            mono ? "font-mono text-[11px]" : ""
          }`}
          title={value}
        >
          {value}
        </div>
      </div>
    </div>
  );
}

function EditControlsView({
  edits,
  activePreset,
  ratios,
  onChange,
  onApplyPreset,
  onReset,
  onExport,
}: {
  edits: EditState;
  activePreset: PresetId | null;
  ratios: AspectRatio[];
  onChange: <K extends keyof EditState>(key: K, value: EditState[K]) => void;
  onApplyPreset: (id: PresetId) => void;
  onReset: () => void;
  onExport: () => void;
}) {
  return (
    <div className="space-y-2">
      <CollapsibleSection title="Light" defaultOpen>
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

      <CollapsibleSection title="Rotate & Crop">
        <div className="grid grid-cols-2 gap-2">
          <button
            type="button"
            onClick={() => onChange("orientation", rotateOrientation(edits.orientation, -90))}
            className="flex items-center justify-center gap-1.5 rounded-lg border border-white/10 bg-white/5 py-2 text-xs text-white/75 hover:bg-white/10"
          >
            <RotateCcw size={14} />
            Left 90°
          </button>
          <button
            type="button"
            onClick={() => onChange("orientation", rotateOrientation(edits.orientation, 90))}
            className="flex items-center justify-center gap-1.5 rounded-lg border border-white/10 bg-white/5 py-2 text-xs text-white/75 hover:bg-white/10"
          >
            <RotateCw size={14} />
            Right 90°
          </button>
        </div>
        {edits.orientation !== 0 ? (
          <button
            type="button"
            onClick={() => onChange("orientation", 0)}
            className="w-full rounded-lg border border-white/10 bg-white/5 py-2 text-xs text-white/70 hover:bg-white/10"
          >
            Reset rotation ({edits.orientation}°)
          </button>
        ) : null}
        <button
          type="button"
          onClick={() => onChange("cropMode", !edits.cropMode)}
          className={`w-full rounded-lg border py-2 text-xs font-medium transition ${
            edits.cropMode
              ? "border-[#75c9a3] bg-[#75c9a3]/18 text-white"
              : "border-white/10 bg-white/5 text-white/70 hover:bg-white/10"
          }`}
        >
          {edits.cropMode ? "Crop overlay on" : "Crop overlay"}
        </button>
        <div className="flex flex-wrap gap-1.5">
          {ratios.map((r) => (
            <button
              key={r}
              type="button"
              onClick={() => onChange("aspectRatio", r)}
              className={`rounded-md border px-2 py-1 text-[10px] ${
                edits.aspectRatio === r
                  ? "border-[#75c9a3] bg-[#75c9a3]/18 text-white"
                  : "border-white/10 text-white/55 hover:bg-white/8"
              }`}
            >
              {r === "free" ? "Free" : r}
            </button>
          ))}
        </div>
        <SliderControl
          label="Straighten"
          value={edits.rotation}
          min={-45}
          max={45}
          step={0.5}
          onChange={(v) => onChange("rotation", v)}
          format={(v) => `${v.toFixed(1)}°`}
        />
      </CollapsibleSection>

      <CollapsibleSection title="Presets" defaultOpen={false}>
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

      <div className="sticky bottom-0 space-y-2 border-t border-white/6 bg-[#0d1520]/95 pt-3 backdrop-blur">
        <button
          type="button"
          onClick={onReset}
          className="flex w-full items-center justify-center gap-2 rounded-lg border border-white/10 bg-white/5 py-2 text-sm text-white/75 hover:bg-white/10"
        >
          <RotateCcw size={14} />
          Reset edits
        </button>
        <button
          type="button"
          onClick={onExport}
          className="flex w-full items-center justify-center gap-2 rounded-xl bg-[#087bff] py-2.5 text-sm font-semibold text-white shadow-lg shadow-[#087bff]/25 hover:bg-[#1e88ff]"
        >
          <Download size={15} />
          Export…
        </button>
      </div>
    </div>
  );
}

function formatDate(iso: string | null | undefined): string {
  if (!iso) return "—";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "—";
  return date.toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

function leafFolder(path: string): string {
  const normalized = path.replace(/\\/g, "/");
  const parts = normalized.split("/").filter(Boolean);
  if (parts.length <= 1) return normalized;
  return parts[parts.length - 1] ?? normalized;
}
