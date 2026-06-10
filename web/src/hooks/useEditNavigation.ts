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

  const forwardToEdit = useCallback(() => {
    if (mode !== "library") return;
    window.history.forward();
  }, [mode]);

  useEffect(() => {
    const onPopState = () => {
      setMode(window.history.state?.lumen === "edit" ? "edit" : "library");
    };
    window.addEventListener("popstate", onPopState);
    return () => window.removeEventListener("popstate", onPopState);
  }, [setMode]);

  useEffect(() => {
    const isInteractive = (target: EventTarget | null) => {
      if (!(target instanceof HTMLElement)) return false;
      const tag = target.tagName;
      return (
        tag === "INPUT" ||
        tag === "TEXTAREA" ||
        tag === "SELECT" ||
        tag === "BUTTON" ||
        target.isContentEditable
      );
    };

    const onMouseDown = (event: MouseEvent) => {
      if (isInteractive(event.target)) return;

      // Browser back (X1): leave preview → library
      if (event.button === 3 && mode === "edit") {
        event.preventDefault();
        backToLibrary();
        return;
      }

      // Browser forward (X2): re-enter preview when history allows
      if (event.button === 4 && mode === "library") {
        event.preventDefault();
        forwardToEdit();
      }
    };

    const onAuxClick = (event: MouseEvent) => {
      if (isInteractive(event.target)) return;

      if (event.button === 3 && mode === "edit") {
        event.preventDefault();
        backToLibrary();
        return;
      }

      if (event.button === 4 && mode === "library") {
        event.preventDefault();
        forwardToEdit();
      }
    };

    window.addEventListener("mousedown", onMouseDown);
    window.addEventListener("auxclick", onAuxClick);
    return () => {
      window.removeEventListener("mousedown", onMouseDown);
      window.removeEventListener("auxclick", onAuxClick);
    };
  }, [mode, backToLibrary, forwardToEdit]);

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

  return { enterEdit, backToLibrary, forwardToEdit, handleModeChange };
}
