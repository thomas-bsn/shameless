import { useState } from "react";
import { cn } from "@/lib/utils";

const FONDS = [
  "bg-rose-500/25",
  "bg-amber-500/25",
  "bg-emerald-500/25",
  "bg-sky-500/25",
  "bg-violet-500/25",
  "bg-fuchsia-500/25",
  "bg-teal-500/25",
  "bg-orange-500/25",
];

/** Le fond d'un Joueur sans photo : toujours le même pour un même prénom. */
export function fondDesInitiales(prenom: string): string {
  let hash = 0;
  for (const c of prenom) hash = (hash * 31 + c.codePointAt(0)!) >>> 0;
  return FONDS[hash % FONDS.length]!;
}

/** Les initiales d'un Joueur : la lettre de ses deux premiers mots, ou ses deux premières lettres. */
export function initiales(prenom: string): string {
  const mots = prenom.trim().split(/\s+/).filter(Boolean);
  const lettres = mots.length > 1 ? mots[0]!.charAt(0) + mots[1]!.charAt(0) : (mots[0] ?? "?").slice(0, 2);
  return lettres.toUpperCase();
}

/**
 * L'avatar d'un Joueur : la photo Discord du compte lié, sinon ses initiales sur un fond coloré
 * (aussi quand la photo ne charge pas).
 */
export function AvatarJoueur({
  prenom,
  avatarUrl,
  className,
}: {
  prenom: string;
  avatarUrl?: string | null;
  /** La taille et la taille du texte, par exemple « size-10 text-lg ». */
  className: string;
}) {
  const [enEchec, setEnEchec] = useState<string | null>(null);
  if (avatarUrl && enEchec !== avatarUrl) {
    return (
      <img
        src={avatarUrl}
        alt={`Photo de ${prenom}`}
        loading="lazy"
        onError={() => setEnEchec(avatarUrl)}
        className={cn("shrink-0 rounded-full object-cover", className)}
      />
    );
  }
  return (
    <span
      aria-hidden
      className={cn("flex shrink-0 items-center justify-center rounded-full font-semibold", fondDesInitiales(prenom), className)}
    >
      {initiales(prenom)}
    </span>
  );
}
