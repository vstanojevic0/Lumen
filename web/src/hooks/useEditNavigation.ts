import { useCallback, useEffect } from "react";
import type { AppMode } from "../types";

export function useEditNavigation(
  mode: AppMode,
  setMode: (mode: AppMode) => void,
) {
  const enterEdit = useCallback(() => {
    setMode("edit");
    if (window.history.state?.lumen !== "edit") {
      window.history.pushState({ lumen: "edit" }, "");
    }
  }, [setMode]);

  const backToLibrary = useCallback(() => {
    if (mode !== "edit") return;
    if (window.history.state?.lumen === "edit") {
      window.history.back();
    } else {
      setMode("library");
    }
  }, [mode, setMode]);

  useEffect(() => {
    const onPopState = () => setMode("library");
    window.addEventListener("popstate", onPopState);
    return () => window.removeEventListener("popstate", onPopState);
  }, [setMode]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key !== "Escape" || mode !== "edit") return;
      event.preventDefault();
      backToLibrary();
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [mode, backToLibrary]);

  const handleModeChange = useCallback(
    (next: AppMode) => {
      if (next === "library" && mode === "edit") {
        backToLibrary();
        return;
      }
      if (next === "edit") {
        enterEdit();
        return;
      }
      setMode(next);
    },
    [mode, setMode, enterEdit, backToLibrary],
  );

  return { enterEdit, backToLibrary, handleModeChange };
}
