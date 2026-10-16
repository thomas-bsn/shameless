/**
 * Les URL du front que l'organisateur envoie (ADR 0010) : un Lien capitaine ouvre la page capitaine
 * de la Draft de champions, le Lien spectateur sa page spectateur. Le secret vient de l'API.
 */
export function lienCapitaineUrl(lien: string, origin: string = window.location.origin): string {
  return `${origin}/draft/capitaine/${encodeURIComponent(lien)}`;
}

/** La page est ouverte par un lien d'accès (capitaine ou spectateur) : pas de session, pas de menu de connexion. */
export function pageParLien(pathname: string): boolean {
  return /^\/draft\/(capitaine|spectateur)\/[^/]+$/.test(pathname);
}

export function lienSpectateurUrl(lien: string, origin: string = window.location.origin): string {
  return `${origin}/draft/spectateur/${encodeURIComponent(lien)}`;
}
