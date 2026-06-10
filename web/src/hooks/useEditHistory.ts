import { useCallback, useState } from "react";
import { defaultEditValues, type EditState } from "../types";

const MAX_HISTORY = 40;

export function useEditHistory(initial?: Partial<EditState>) {
  const [edits, setEdits] = useState<EditState>({ ...defaultEditValues(), ...initial });
  const [past, setPast] = useState<EditState[]>([]);
  const [future, setFuture] = useState<EditState[]>([]);
  const [activePreset, setActivePreset] = useState<string | null>(null);

  const pushHistory = useCallback((prev: EditState) => {
    setPast((p) => [...p.slice(-MAX_HISTORY + 1), prev]);
    setFuture([]);
  }, []);

  const update = useCallback(
    <K extends keyof EditState>(key: K, value: EditState[K]) => {
      setEdits((current) => {
        pushHistory(current);
        return { ...current, [key]: value };
      });
      setActivePreset(null);
    },
    [pushHistory],
  );

  const patch = useCallback(
    (partial: Partial<EditState>) => {
      setEdits((current) => {
        pushHistory(current);
        return { ...current, ...partial };
      });
      setActivePreset(null);
    },
    [pushHistory],
  );

  const replace = useCallback(
    (next: EditState, presetId: string | null = null) => {
      setEdits((current) => {
        pushHistory(current);
        return next;
      });
      setActivePreset(presetId);
    },
    [pushHistory],
  );

  const reset = useCallback(() => {
    setEdits((current) => {
      pushHistory(current);
      return defaultEditValues();
    });
    setActivePreset("original");
  }, [pushHistory]);

  const undo = useCallback(() => {
    setPast((p) => {
      if (p.length === 0) return p;
      const previous = p[p.length - 1];
      setEdits((current) => {
        setFuture((f) => [current, ...f]);
        return previous;
      });
      setActivePreset(null);
      return p.slice(0, -1);
    });
  }, []);

  const redo = useCallback(() => {
    setFuture((f) => {
      if (f.length === 0) return f;
      const [next, ...rest] = f;
      setEdits((current) => {
        setPast((p) => [...p, current]);
        return next;
      });
      setActivePreset(null);
      return rest;
    });
  }, []);

  return {
    edits,
    activePreset,
    update,
    patch,
    replace,
    reset,
    undo,
    redo,
    canUndo: past.length > 0,
    canRedo: future.length > 0,
    setActivePreset,
  };
}
