/**
 * Validation des champs facultatifs du wizard d'installation (#71, spec Uchuu #72).
 * Fonctions pures : elles ne font aucun appel réseau et ne répètent jamais la valeur saisie
 * dans leurs messages (la clé Riot et les identifiants d'une URL sont des secrets).
 */

export type Validation<T> = { ok: true; valeur: T } | { ok: false; erreur: string };

export type ValidationAdresse =
  | { ok: true; valeur: string; avertissements: string[] }
  | { ok: false; erreur: string };

/** Identifiant Discord : 17 à 20 chiffres. Motif et formulation uniques pour tout le wizard. */
export const ID_DISCORD = /^\d{17,20}$/;
export const FORMAT_ID_DISCORD = "un nombre de 17 à 20 chiffres";
export const estIdDiscord = (valeur: string): boolean => ID_DISCORD.test(valeur);

export const FUSEAU_PAR_DEFAUT = "Europe/Paris";

const AVERTISSEMENT_LOCAL =
  "Cette adresse est locale ou privée : elle sera injoignable depuis le conteneur de l'API, sauf si le bot est routable à cette adresse depuis lui.";

function estIpv4Privee(hote: string): boolean {
  const m = /^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})$/.exec(hote);
  if (!m) return false;
  const [a, b] = [Number(m[1]), Number(m[2])];
  return a === 10 || a === 127 || (a === 172 && b >= 16 && b <= 31) || (a === 192 && b === 168) || (a === 169 && b === 254);
}

/** localhost, IP privée, nom `.local`/`.localhost` ou nom sans point (service Docker). */
function estHoteLocal(hote: string): boolean {
  const h = hote.toLowerCase().replace(/^\[|\]$/g, "");
  return (
    h === "localhost" ||
    h === "::1" ||
    h.endsWith(".localhost") ||
    h.endsWith(".local") ||
    estIpv4Privee(h) ||
    (!h.includes(".") && !h.includes(":"))
  );
}

/** L'adresse publique du bot Discord (Uchuu) : `https://bot.example.org`, sans chemin ni « / » final. */
export function validerAdresseBot(saisie: string): ValidationAdresse {
  const adresse = saisie.trim();
  if (adresse === "") return { ok: false, erreur: "Indiquez l'adresse de votre bot, ou désactivez l'intégration." };

  let url: URL;
  try {
    url = new URL(adresse);
  } catch {
    return { ok: false, erreur: "Cette adresse n'est pas valide. Exemple : https://bot.example.org" };
  }
  if (url.protocol !== "https:" && url.protocol !== "http:") {
    return { ok: false, erreur: "L'adresse doit commencer par https:// (http:// n'est accepté que pour localhost ou un réseau privé)." };
  }
  if (url.username !== "" || url.password !== "") {
    return { ok: false, erreur: "L'adresse ne doit pas contenir d'identifiants (utilisateur ou mot de passe)." };
  }
  if (url.search !== "" || url.hash !== "" || /[?#]/.test(adresse)) {
    return { ok: false, erreur: "L'adresse ne doit pas contenir de paramètres ni d'ancre (? ou #)." };
  }
  if (adresse.endsWith("/") && url.pathname === "/") {
    return { ok: false, erreur: "Retirez le « / » final de l'adresse." };
  }
  if (url.pathname !== "/") {
    return { ok: false, erreur: "L'adresse ne doit pas contenir de chemin : indiquez seulement l'hôte (et le port)." };
  }

  const local = estHoteLocal(url.hostname);
  if (url.protocol === "http:" && !local) {
    return { ok: false, erreur: "L'adresse doit commencer par https:// (http:// n'est accepté que pour localhost ou un réseau privé)." };
  }
  return { ok: true, valeur: `${url.protocol}//${url.host}`, avertissements: local ? [AVERTISSEMENT_LOCAL] : [] };
}

/** La clé Riot est facultative : vide = les fonctions qui en dépendent sont désactivées. */
export function validerCleRiot(saisie: string): Validation<string> {
  const cle = saisie.trim();
  if (cle === "") return { ok: true, valeur: "" };
  if (!cle.startsWith("RGAPI-")) return { ok: false, erreur: "Une clé Riot commence par « RGAPI- »." };
  if (!/^RGAPI-[0-9A-Za-z]+(-[0-9A-Za-z]+)*$/.test(cle)) {
    return { ok: false, erreur: "Cette clé Riot est incomplète ou contient des caractères non valides (espaces, symboles)." };
  }
  return { ok: true, valeur: cle };
}

/** Fuseau IANA de l'Instance ; vide = Europe/Paris. Renvoie la casse canonique. */
export function validerFuseau(saisie: string): Validation<string> {
  const fuseau = saisie.trim();
  if (fuseau === "") return { ok: true, valeur: FUSEAU_PAR_DEFAUT };
  if (!/^[A-Za-z][A-Za-z0-9_+-]*(\/[A-Za-z0-9_+-]+)*$/.test(fuseau)) {
    return { ok: false, erreur: "Ce fuseau horaire n'est pas valide. Exemple : Europe/Paris" };
  }
  try {
    return { ok: true, valeur: new Intl.DateTimeFormat("en-US", { timeZone: fuseau }).resolvedOptions().timeZone };
  } catch {
    return { ok: false, erreur: "Ce fuseau horaire est inconnu. Utilisez un nom IANA, par exemple Europe/Paris." };
  }
}
