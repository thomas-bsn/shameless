import { Link } from "@tanstack/react-router";
import type { Schemas } from "@/api/client";
import { sideName } from "@/components/game-equipe-table";
import { sidesOf } from "@/components/game-scoreboard";
import { IconeChampion, IconeRune } from "@/components/icones-de-jeu/icones-catalogue";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { sideBorderClass, sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

type Participant = Schemas["GameHistoryParticipantDto"];

/**
 * Les runes d'un joueur : la clé de voûte sous le portrait, trois runes de l'arbre principal et deux de
 * l'arbre secondaire (les fragments ne sont pas affichés). Sans donnée ou id inconnu, des emplacements neutres.
 */
function RunesDuJoueur({ p, side }: { p: Participant; side: "blue" | "red" }) {
  const perks = p.details?.perks ?? p.stats?.perks ?? null;
  const arbrePrincipal = p.details?.perkPrimaryStyle ?? p.stats?.perkPrimaryStyle;
  const arbreSecondaire = p.details?.perkSubStyle ?? p.stats?.perkSubStyle;
  const rune = (i: number) => perks?.[i] ?? null;

  return (
    <li
      role="group"
      aria-label={`Runes de ${p.prenom}`}
      className="flex flex-col items-center gap-2 rounded-xl bg-muted/30 px-2 py-3"
    >
      <div className="relative">
        <IconeChampion
          championId={p.championId}
          className={cn("size-16 rounded-xl border-2 bg-muted sm:size-20", sideBorderClass(side))}
        />
        <div role="group" aria-label="Clé de voûte" className="absolute -bottom-5 left-1/2 -translate-x-1/2">
          <IconeRune runeId={rune(0)} className="size-10 rounded-full border-2 border-background bg-background" />
        </div>
      </div>
      <Link
        to="/joueurs/$playerId"
        params={{ playerId: String(p.playerId) }}
        className="mt-5 max-w-full truncate text-sm font-semibold underline underline-offset-2"
      >
        {p.prenom}
      </Link>
      <div role="group" aria-label="Arbre principal" className="flex items-center gap-1">
        <IconeRune runeId={arbrePrincipal} className="size-6 opacity-70" />
        {[1, 2, 3].map((i) => (
          <IconeRune key={i} runeId={rune(i)} className="size-7 rounded-full" />
        ))}
      </div>
      <div role="group" aria-label="Arbre secondaire" className="flex items-center gap-1">
        <IconeRune runeId={arbreSecondaire} className="size-6 opacity-70" />
        {[4, 5].map((i) => (
          <IconeRune key={i} runeId={rune(i)} className="size-6 rounded-full" />
        ))}
      </div>
    </li>
  );
}

/** L'onglet Runes : les dix joueurs, côté bleu puis côté rouge. */
export function OngletRunes({ game }: { game: Schemas["GameRecapResponse"] }) {
  return (
    <div className="flex flex-col gap-4">
      {sidesOf(game).map((c) => (
        <Card key={c.side}>
          <CardHeader>
            <CardTitle className={cn("text-sm", sideTextClass(c.side))}>
              Équipe {c.team} · {sideName(c.side)} side
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-5">
              {c.players.map((p) => (
                <RunesDuJoueur key={p.playerId} p={p} side={c.side} />
              ))}
            </ul>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
