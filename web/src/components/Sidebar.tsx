import {
  Folder,
  FolderOpen,
  Heart,
  Image,
  MapPin,
  Settings,
  Star,
  Trash2,
  Upload,
  Users,
} from "lucide-react";

const navMain = [
  { icon: Image, label: "All Photos", active: true, count: "12,452" },
  { icon: Heart, label: "Favorites" },
  { icon: Folder, label: "Albums" },
  { icon: Users, label: "People" },
  { icon: MapPin, label: "Places" },
  { icon: Upload, label: "Imports" },
];

const library = ["Recent", "Hidden", "Trash"];
const albums = [
  "Road Trip Iceland",
  "Japan 2024",
  "Summer Days",
  "Family",
  "City Nights",
  "Portraits",
];
const folders = ["Pictures", "2026", "2025", "Downloads"];

export function Sidebar() {
  return (
    <aside className="glass flex h-full w-[248px] shrink-0 flex-col border-r border-white/8">
      <div className="border-b border-white/8 px-4 py-5">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-[#3b9bff] to-[#2a6fd4] text-lg font-bold text-white shadow-lg shadow-[#3b9bff]/25">
            L
          </div>
          <div>
            <div className="text-lg font-semibold tracking-tight">Lumen</div>
            <div className="text-[11px] text-white/45">Photo library</div>
          </div>
        </div>
      </div>

      <div className="flex-1 overflow-y-auto px-2 py-3">
        <nav className="space-y-0.5">
          {navMain.map((item) => (
            <button
              key={item.label}
              type="button"
              className={`flex w-full items-center gap-2.5 rounded-lg px-3 py-2 text-[13px] transition ${
                item.active
                  ? "bg-[#3b9bff]/15 text-white ring-1 ring-[#3b9bff]/40"
                  : "text-white/70 hover:bg-white/6 hover:text-white"
              }`}
            >
              <item.icon size={16} className={item.active ? "text-[#7eb8ff]" : "text-white/45"} />
              <span className="flex-1 text-left">{item.label}</span>
              {item.count ? (
                <span className="rounded-md bg-white/8 px-1.5 py-0.5 text-[10px] text-white/55">
                  {item.count}
                </span>
              ) : null}
            </button>
          ))}
        </nav>

        <SectionLabel>Library</SectionLabel>
        {library.map((name) => (
          <SideRow key={name} label={name} icon={name === "Trash" ? Trash2 : Star} />
        ))}

        <SectionLabel>Albums</SectionLabel>
        {albums.map((name) => (
          <SideRow key={name} label={name} indent />
        ))}

        <SectionLabel>Folders</SectionLabel>
        <SideRow label="Pictures" icon={FolderOpen} />
        {folders.slice(1).map((name) => (
          <SideRow key={name} label={name} indent />
        ))}
      </div>

      <div className="border-t border-white/8 p-4">
        <div className="mb-2 flex justify-between text-[11px] text-white/50">
          <span>Storage</span>
          <span>1.28 TB of 2 TB</span>
        </div>
        <div className="h-1.5 overflow-hidden rounded-full bg-white/10">
          <div className="h-full w-[64%] rounded-full bg-gradient-to-r from-[#3b9bff] to-[#2a6fd4]" />
        </div>
        <button
          type="button"
          className="mt-3 flex w-full items-center justify-center gap-2 rounded-lg py-2 text-xs text-white/50 hover:bg-white/6 hover:text-white/80"
        >
          <Settings size={14} />
          Settings
        </button>
      </div>
    </aside>
  );
}

function SectionLabel({ children }: { children: string }) {
  return (
    <div className="mt-5 mb-1.5 px-3 text-[10px] font-semibold tracking-widest text-white/35 uppercase">
      {children}
    </div>
  );
}

function SideRow({
  label,
  icon: Icon = Folder,
  indent,
}: {
  label: string;
  icon?: typeof Folder;
  indent?: boolean;
}) {
  return (
    <button
      type="button"
      className={`flex w-full items-center gap-2 rounded-lg py-1.5 text-[13px] text-white/65 hover:bg-white/6 hover:text-white ${
        indent ? "pl-8 pr-3" : "px-3"
      }`}
    >
      <Icon size={14} className="shrink-0 text-white/40" />
      <span className="truncate text-left">{label}</span>
    </button>
  );
}
