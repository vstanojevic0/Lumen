import { useEffect, useState } from "react";
import { isLumenHost, lumenCall } from "./hostBridge";

export function useThumbnail(path: string | undefined, enabled = true): string {
  const [src, setSrc] = useState("");

  useEffect(() => {
    if (!enabled || !path) {
      setSrc("");
      return;
    }

    if (!isLumenHost()) {
      setSrc("");
      return;
    }

    let cancelled = false;
    void lumenCall<{ dataUrl: string }>("getThumbnail", { path })
      .then((r) => {
        if (!cancelled) setSrc(r.dataUrl);
      })
      .catch(() => {
        if (!cancelled) setSrc("");
      });

    return () => {
      cancelled = true;
    };
  }, [path, enabled]);

  return src;
}

export function usePreview(path: string | undefined, enabled = true): string {
  const [src, setSrc] = useState("");

  useEffect(() => {
    if (!enabled || !path) {
      setSrc("");
      return;
    }

    if (!isLumenHost()) {
      setSrc("");
      return;
    }

    let cancelled = false;
    void lumenCall<{ dataUrl: string }>("getPreview", { path })
      .then((r) => {
        if (!cancelled) setSrc(r.dataUrl);
      })
      .catch(() => {
        if (!cancelled) setSrc("");
      });

    return () => {
      cancelled = true;
    };
  }, [path, enabled]);

  return src;
}
