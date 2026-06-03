interface SliderControlProps {
  label: string;
  value: number;
  min: number;
  max: number;
  step?: number;
  onChange: (value: number) => void;
  format?: (v: number) => string;
}

export function SliderControl({
  label,
  value,
  min,
  max,
  step = 1,
  onChange,
  format = (v) => (step < 1 ? v.toFixed(2) : String(Math.round(v))),
}: SliderControlProps) {
  return (
    <div className="group">
      <div className="mb-1.5 flex items-center justify-between text-xs">
        <span className="text-white/74">{label}</span>
        <span className="tabular-nums text-white/68">{format(value)}</span>
      </div>
      <input
        type="range"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={(e) => onChange(parseFloat(e.target.value))}
        className="h-1.5 w-full cursor-pointer appearance-none rounded-full bg-white/12 accent-[#d8e7ff]"
      />
    </div>
  );
}
