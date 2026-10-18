import { useEffect, useSyncExternalStore } from "react";

export type Theme = "dark" | "light";

const STORAGE_KEY = "shameless-theme";
const listeners = new Set<() => void>();
// Repli quand le navigateur refuse le stockage : le choix tient jusqu'au rechargement.
let enMemoire: Theme | null = null;

/** Le thème choisi (sombre par défaut), lu dans le navigateur. */
export function readTheme(): Theme {
  try {
    const stocke = localStorage.getItem(STORAGE_KEY);
    return stocke === "light" ? "light" : "dark";
  } catch {
    // Stockage indisponible : on retombe sur le repli en mémoire.
    return enMemoire ?? "dark";
  }
}

export function writeTheme(theme: Theme): void {
  try {
    localStorage.setItem(STORAGE_KEY, theme);
    enMemoire = null;
  } catch {
    // Choix gardé en mémoire seulement.
    enMemoire = theme;
  }
  for (const listener of listeners) listener();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** Le thème courant et son réglage ; pose aussi la classe `dark` sur <html>. */
export function useTheme(): [Theme, (theme: Theme) => void] {
  const theme = useSyncExternalStore(subscribe, readTheme, () => "dark" as const);
  useEffect(() => {
    document.documentElement.classList.toggle("dark", theme === "dark");
  }, [theme]);
  return [theme, writeTheme];
}
