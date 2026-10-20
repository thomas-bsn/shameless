import type { Schemas } from "@/api/client";
import { parRole } from "@/lib/picker";
import { useChampions } from "@/lib/draft-champions-queries";
import { sideBorderClass, sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";
import { AvatarJoueur } from "@/components/avatar-joueur";

type Game = Schemas["SeriesGameResponse"];
type Champion = Schemas["ChampionResponse"];

/**
 * Les champions réellement joués par chaque joueur d'une game, d'après le rapport du Companion.
 * Rien sans rapport (game en attente ou résultat saisi à la main).
 */
export function ChampionsJoues({ game }: { game: Game }) {
  const champions = useChampions();
  if (game.championsJoues.length === 0) return null;

  const parJoueur = new Map(game.championsJoues.map((c) => [c.playerId, c.championId]));
  const parId = new Map((champions.data ?? []).map((c) => [c.id, c]));

  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {(["A", "B"] as const).map((equipe) => {
        const joueurs = parRole(equipe === "A" ? game.teamA : game.teamB);
        const side = equipe === "A" ? game.teamASide : game.teamBSide;
        return (
          <ul
            key={equipe}
            aria-label={`Champions joués par l'Équipe ${equipe}`}
            className={cn("flex flex-col gap-1 border-l-2 pl-2", sideBorderClass(side))}
          >
            <li className={cn("text-xs font-medium", sideTextClass(side))}>Équipe {equipe}</li>
            {joueurs.map((j) => {
              const championId = parJoueur.get(j.playerId);
              return (
                <li key={j.playerId} className="flex items-center gap-2">
                  <ChampionJoue championId={championId} champion={championId ? parId.get(championId) : undefined} />
                  <AvatarJoueur prenom={j.prenom} avatarUrl={j.avatarUrl} className="size-5 text-[9px]" />
                  <span className="font-medium">{j.prenom}</span>
                </li>
              );
            })}
          </ul>
        );
      })}
    </div>
  );
}

function ChampionJoue({ championId, champion }: { championId?: string; champion?: Champion }) {
  return (
    <span className="flex min-w-32 items-center gap-2 text-muted-foreground">
      {champion ? (
        <img src={champion.imageUrl} alt={champion.name} className="size-7 rounded" />
      ) : (
        <span aria-hidden className="size-7 rounded bg-muted" />
      )}
      <span>{champion?.name ?? championId ?? "—"}</span>
    </span>
  );
}
