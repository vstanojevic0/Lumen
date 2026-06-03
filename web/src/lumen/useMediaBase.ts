import { useEffect, useState } from "react";
import { getMediaBase } from "./mediaUrls";

/** Re-render when the desktop host injects the loopback media server URL. */
export function useMediaBase(): string | null {
  const [base, setBase] = useState(getMediaBase);

  useEffect(() => {
    const sync = () => {
      const next = getMediaBase();
      setBase((prev) => (prev === next ? prev : next));
    };

    sync();
    window.addEventListener("lumen:hostReady", sync);
    window.addEventListener("lumen:mediaReady", sync);
    const interval = window.setInterval(sync, 250);

    return () => {
      window.removeEventListener("lumen:hostReady", sync);
      window.removeEventListener("lumen:mediaReady", sync);
      window.clearInterval(interval);
    };
  }, []);

  return base;
}
