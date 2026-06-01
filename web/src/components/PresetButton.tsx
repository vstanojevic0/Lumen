interface PresetButtonProps {
  label: string;
  active?: boolean;
  onClick: () => void;
}

export function PresetButton({ label, active, onClick }: PresetButtonProps) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-lg border px-2.5 py-2 text-[11px] font-medium transition ${
        active
          ? "border-[#3b9bff] bg-[#3b9bff]/20 text-white"
          : "border-white/10 bg-white/5 text-white/70 hover:border-white/20 hover:bg-white/10"
      }`}
    >
      {label}
    </button>
  );
}
