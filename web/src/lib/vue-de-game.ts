import { useSyncExternalStore } from "react";

/** La vue de la page d'une game : le scoreboard en liste ou la vue en cibles. */
export type VueDeGame = "liste" | "cibles";

const STORAGE_KEY = "shameless-vue-de-game";
const listeners = new Set<() => void>();
// Repli quand le navigateur refuse le stockage : le choix tient jusqu'au rechargement.
let enMemoire: VueDeGame | null = null;

export function readVueDeGame(): VueDeGame {
  try {
    return localStorage.getItem(STORAGE_KEY) === "cibles" ? "cibles" : "liste";
  } catch {
    return enMemoire ?? "liste";
  }
}

export function writeVueDeGame(vue: VueDeGame): void {
  try {
    localStorage.setItem(STORAGE_KEY, vue);
    enMemoire = null;
  } catch {
    enMemoire = vue;
  }
  for (const listener of listeners) listener();
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

export function useVueDeGame(): [VueDeGame, (vue: VueDeGame) => void] {
  const vue = useSyncExternalStore(subscribe, readVueDeGame, () => "liste" as const);
  return [vue, writeVueDeGame];
}
