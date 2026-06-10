import { useEffect, useState } from "react";
import { isLumenHost, lumenCall, type WebPhotoDetailsDto } from "../lumen/hostBridge";

export function usePhotoDetails(path: string | undefined) {
  const [details, setDetails] = useState<WebPhotoDetailsDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!path || !isLumenHost()) {
      setDetails(null);
      setLoading(false);
      setError(null);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    void lumenCall<WebPhotoDetailsDto | null>("getPhotoDetails", { path })
      .then((result) => {
        if (!cancelled) setDetails(result);
      })
      .catch((err: unknown) => {
        if (!cancelled) {
          setDetails(null);
          setError(err instanceof Error ? err.message : "Failed to load photo info");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [path]);

  return { details, loading, error };
}
