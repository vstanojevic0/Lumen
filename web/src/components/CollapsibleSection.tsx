import { ChevronDown } from "lucide-react";
import { useState, type ReactNode } from "react";

interface CollapsibleSectionProps {
  title: string;
  defaultOpen?: boolean;
  children: ReactNode;
  badge?: string;
}

export function CollapsibleSection({
  title,
  defaultOpen = true,
  children,
  badge,
}: CollapsibleSectionProps) {
  const [open, setOpen] = useState(defaultOpen);

  return (
    <section className="glass overflow-hidden rounded-xl">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center justify-between px-3.5 py-2.5 text-left text-xs font-semibold tracking-wide text-white/80 uppercase hover:bg-white/5"
      >
        <span className="flex items-center gap-2">
          {title}
          {badge ? (
            <span className="rounded-md bg-[#3b9bff]/20 px-1.5 py-0.5 text-[10px] font-medium text-[#9ec9ff] normal-case">
              {badge}
            </span>
          ) : null}
        </span>
        <ChevronDown
          size={14}
          className={`text-white/40 transition-transform ${open ? "rotate-180" : ""}`}
        />
      </button>
      {open ? <div className="space-y-3 border-t border-white/6 px-3.5 py-3">{children}</div> : null}
    </section>
  );
}
