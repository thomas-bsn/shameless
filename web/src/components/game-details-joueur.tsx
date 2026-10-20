import { useState } from "react";
import type { Schemas } from "@/api/client";
import { ChampionCarre, type Participant } from "@/components/game-equipe-table";
import { sidesOf } from "@/components/game-scoreboard";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useChampions } from "@/lib/draft-champions-queries";
import { millierLabel, nombreLabel, posteLabel, statLabel } from "@/lib/game";
import { cn } from "@/lib/utils";

type Recap = Schemas["GameRecapResponse"];

function Mesure({ nom, valeur }: { nom: string; valeur: string }) {
  return (
    <div className="flex items-baseline justify-between gap-3 border-b border-border/50 py-1">
      <dt className="text-sm text-muted-foreground">{nom}</dt>
      <dd className="font-mono font-semibold tabular-nums">{valeur}</dd>
    </div>
  );
}

function Section({ titre, children }: { titre: string; children: React.ReactNode }) {
  return (
    <section className="flex flex-col gap-1">
      <h3 className="text-sm font-semibold">{titre}</h3>
      <dl>{children}</dl>
    </section>
  );
}

/**
 * Le détail d'un joueur de la game : balises, stats par minute, dégâts par type.
 * Les sorts lancés et les pings ne sont pas encore transportés par l'API : ils ne sont pas affichés.
 */
export function DetailsDuJoueur({ participant, side }: { participant: Participant; side: "blue" | "red" }) {
  const champions = useChampions();
  const champion = champions.data?.find((c) => c.id === participant.championId);
  const s = participant.stats;
  const d = participant.details;
  const role = posteLabel(d?.role ?? s?.position);

  return (
    <Card role="group" aria-label={`Détails de ${participant.prenom}`}>
      <CardHeader className="flex-row items-center gap-3">
        <ChampionCarre
          championId={participant.championId}
          nom={champion?.name}
          image={champion?.imageUrl}
          niveau={s?.champLevel}
          side={side}
        />
        <CardTitle className="flex flex-col">
          <span>{participant.prenom}</span>
          <span className="text-xs font-normal text-muted-foreground">
            {[champion?.name ?? participant.championId, role].filter(Boolean).join(" · ") || "Champion inconnu"}
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent className="grid gap-6 md:grid-cols-3">
        <Section titre="Vision">
          <Mesure nom="Balises posées" valeur={statLabel(s?.wardsPlaced ?? null)} />
          <Mesure nom="Balises détruites" valeur={statLabel(s?.wardsKilled ?? null)} />
          <Mesure nom="Balises de contrôle" valeur={statLabel(s?.controlWardsBought ?? null)} />
          <Mesure nom="Score de vision" valeur={statLabel(s?.visionScore ?? null)} />
        </Section>
        <Section titre="Par minute">
          <Mesure nom="CS par minute" valeur={nombreLabel(d?.csParMinute ?? null, 1)} />
          <Mesure nom="Or par minute" valeur={nombreLabel(d?.orParMinute ?? null)} />
          <Mesure nom="Dégâts par minute" valeur={nombreLabel(d?.degatsParMinute ?? null)} />
          <Mesure nom="Vision par minute" valeur={nombreLabel(d?.visionParMinute ?? null, 1)} />
        </Section>
        <Section titre="Dégâts par type">
          <Mesure nom="Dégâts physiques" valeur={millierLabel(s?.physicalDamageToChampions ?? null)} />
          <Mesure nom="Dégâts magiques" valeur={millierLabel(s?.magicDamageToChampions ?? null)} />
          <Mesure nom="Dégâts bruts" valeur={millierLabel(s?.trueDamageToChampions ?? null)} />
        </Section>
      </CardContent>
    </Card>
  );
}

/** Le sélecteur des joueurs de la game, puis le détail de celui qui est choisi. */
export function SelecteurDeJoueur({ game }: { game: Recap }) {
  const joueurs = sidesOf(game).flatMap((c) => c.players.map((p) => ({ p, side: c.side })));
  const [choisi, setChoisi] = useState<number | null>(null);
  const courant = joueurs.find((j) => j.p.playerId === choisi) ?? joueurs[0];
  if (!courant) return null;

  return (
    <div className="flex flex-col gap-4">
      <div role="group" aria-label="Joueur" className="flex flex-wrap gap-2">
        {joueurs.map(({ p, side }) => (
          <Button
            key={p.playerId}
            type="button"
            size="sm"
            variant={p.playerId === courant.p.playerId ? "default" : "outline"}
            aria-pressed={p.playerId === courant.p.playerId}
            className={cn(p.playerId !== courant.p.playerId && (side === "red" ? "text-red-400" : "text-sky-400"))}
            onClick={() => setChoisi(p.playerId)}
          >
            {p.prenom}
          </Button>
        ))}
      </div>
      <DetailsDuJoueur participant={courant.p} side={courant.side} />
    </div>
  );
}
