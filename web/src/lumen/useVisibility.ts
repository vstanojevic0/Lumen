import { useEffect, useRef, useState } from "react";

type VisibilityCallback = (visible: boolean) => void;

let sharedObserver: IntersectionObserver | null = null;
const callbacks = new Map<Element, VisibilityCallback>();

function ensureObserver() {
  if (sharedObserver) return;
  sharedObserver = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        callbacks.get(entry.target)?.(entry.isIntersecting);
      }
    },
    { rootMargin: "240px 0px", threshold: 0.01 },
  );
}

export function useVisibility(enabled: boolean, eager = false) {
  const ref = useRef<HTMLDivElement>(null);
  const [visible, setVisible] = useState(eager);

  useEffect(() => {
    if (eager) {
      setVisible(true);
      return;
    }
    if (!enabled) {
      setVisible(false);
      return;
    }

    const element = ref.current;
    if (!element) return;

    ensureObserver();
    const onChange: VisibilityCallback = (next) => setVisible(next);
    callbacks.set(element, onChange);
    sharedObserver!.observe(element);

    return () => {
      callbacks.delete(element);
      sharedObserver?.unobserve(element);
    };
  }, [enabled, eager]);

  return { ref, visible };
}
