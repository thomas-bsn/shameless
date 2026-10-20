import { useSyncExternalStore } from "react";

export type CostMode = "podium" | "escalier";

const STORAGE_KEY = "shameless-cost-mode";
const listeners = new Set<() => void>();
// Repli quand le navigateur refuse le stockage : le choix tient jusqu'au rechargement.
let enMemoire: CostMode | null = null;

/** Le mode choisi pour le classement des costs (podium avec liste par défaut). */
export function readCostMode(): CostMode {
  try {
    const stocke = localStorage.getItem(STORAGE_KEY);
    return stocke === "escalier" ? "escalier" : "podium";
  } catch {
    return enMemoire ?? "podium";
  }
}

export function writeCostMode(mode: CostMode): void {
  try {
    localStorage.setItem(STORAGE_KEY, mode);
    enMemoire = null;
  } catch {
    enMemoire = mode;
  }
  for (const listener of listeners) listener();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useCostMode(): [CostMode, (mode: CostMode) => void] {
  const mode = useSyncExternalStore(subscribe, readCostMode, () => "podium" as const);
  return [mode, writeCostMode];
}
