import { useEffect, useSyncExternalStore } from "react";

/** Les deux thèmes du design « Verre nuit » : sombre par défaut, clair au choix. */
export type Theme = "dark" | "light";

/** Clé du choix mémorisé dans le navigateur ; le script d'index.html la lit avant le premier rendu. */
export const THEME_STORAGE_KEY = "shameless-theme";

const abonnes = new Set<() => void>();

/** Le choix mémorisé dans le navigateur, ou null s'il est absent ou illisible. */
function choixMemorise(): Theme | null {
  try {
    const valeur = localStorage.getItem(THEME_STORAGE_KEY);
    return valeur === "dark" || valeur === "light" ? valeur : null;
  } catch {
    return null;
  }
}

/** Le thème choisi, ou le sombre par défaut. */
export function lireTheme(): Theme {
  return choixMemorise() ?? "dark";
}

/** Applique le thème par la classe « dark » sur <html> : le thème clair est l'absence de classe. */
export function appliquerTheme(theme: Theme): void {
  document.documentElement.classList.toggle("dark", theme === "dark");
}

/** Mémorise le thème dans le navigateur et l'applique tout de suite ; les usagers se resynchronisent. */
export function definirTheme(theme: Theme): void {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, theme);
  } catch {
    // Le navigateur refuse le stockage (mode privé) : le choix vaut pour la session en cours.
  }
  appliquerTheme(theme);
  for (const abonne of abonnes) abonne();
}

function souscrire(abonne: () => void): () => void {
  abonnes.add(abonne);
  return () => {
    abonnes.delete(abonne);
  };
}

/** Le thème courant et de quoi le changer ; tous les usagers restent synchronisés. */
export function useTheme(): [Theme, (theme: Theme) => void] {
  const theme = useSyncExternalStore(souscrire, lireTheme);
  useEffect(() => appliquerTheme(theme), [theme]);
  return [theme, definirTheme];
}
