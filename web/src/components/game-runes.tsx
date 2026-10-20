import type { Schemas } from "@/api/client";
import { sideName } from "@/components/game-equipe-table";
import { IconeDeJeu } from "@/components/icone-de-jeu";
import { sidesOf } from "@/components/game-scoreboard";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useCatalogue } from "@/lib/catalogue";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

type Participant = Schemas["GameHistoryParticipantDto"];

/** Les six runes et les deux arbres d'un joueur ; sans donnée, des emplacements vides. */
function RunesDuJoueur({ p }: { p: Participant }) {
  const catalogue = useCatalogue();
  const perks = p.details?.perks ?? p.stats?.perks ?? null;
  const arbres = [
    p.details?.perkPrimaryStyle ?? p.stats?.perkPrimaryStyle,
    p.details?.perkSubStyle ?? p.stats?.perkSubStyle,
  ];
  const runes = Array.from({ length: 6 }, (_, i) => perks?.[i] ?? null);

  return (
    <li role="group" aria-label={`Runes de ${p.prenom}`} className="flex items-center gap-3 rounded-lg bg-muted/30 px-3 py-2">
      <span className="w-24 truncate text-sm font-medium">{p.prenom}</span>
      <div className="flex gap-1">
        {arbres.map((id, i) => (
          <IconeDeJeu key={i} id={id} element={id == null ? undefined : catalogue.arbre(id)} className="size-8" />
        ))}
      </div>
      <div className="flex gap-1">
        {runes.map((id, i) => (
          <IconeDeJeu key={i} id={id} element={id == null ? undefined : catalogue.rune(id)} className="size-8" />
        ))}
      </div>
    </li>
  );
}

/** L'onglet Runes : les dix joueurs, côté bleu puis côté rouge. */
export function OngletRunes({ game }: { game: Schemas["GameRecapResponse"] }) {
  return (
    <div className="grid gap-4 lg:grid-cols-2">
      {sidesOf(game).map((c) => (
        <Card key={c.side}>
          <CardHeader>
            <CardTitle className={cn("text-sm", sideTextClass(c.side))}>
              Équipe {c.team} · {sideName(c.side)} side
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ul className="flex flex-col gap-1">
              {c.players.map((p) => (
                <RunesDuJoueur key={p.playerId} p={p} />
              ))}
            </ul>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
