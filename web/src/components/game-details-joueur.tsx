import { useState, type ReactNode } from "react";
import { Coins, Eye, EyeOff, Flame, Shield, Sword } from "lucide-react";
import type { Schemas } from "@/api/client";
import { ChampionCarre, type Participant } from "@/components/game-equipe-table";
import { sidesOf } from "@/components/game-scoreboard";
import { IconePing, type Ping } from "@/components/icones-de-jeu/icone-ping";
import { IconeCapacite, IconeSort, type ToucheDeCapacite } from "@/components/icones-de-jeu/icones-catalogue";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useChampions } from "@/lib/draft-champions-queries";
import { millierLabel, nombreLabel, posteLabel, statLabel } from "@/lib/game";
import { cn } from "@/lib/utils";

type Recap = Schemas["GameRecapResponse"];
type Details = NonNullable<Participant["details"]>;

/** Une pastille ronde colorée portant une icône. */
function Pastille({ className, children }: { className: string; children: ReactNode }) {
  return (
    <span aria-hidden className={cn("flex size-8 items-center justify-center rounded-full [&>svg]:size-4", className)}>
      {children}
    </span>
  );
}

/** Une mesure : son icône, sa valeur en grand et son nom en petit. */
function Mesure({ nom, valeur, icone }: { nom: string; valeur: string; icone: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-1 text-center">
      {icone}
      <dl className="flex flex-col-reverse items-center">
        <dt className="text-xs text-muted-foreground">{nom}</dt>
        <dd className="font-mono text-xl font-semibold tabular-nums">{valeur}</dd>
      </dl>
    </div>
  );
}

function Section({ titre, children, className }: { titre: string; children: ReactNode; className?: string }) {
  return (
    <section className="flex flex-col gap-3">
      <h3 className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">{titre}</h3>
      <div className={cn("grid gap-3", className)}>{children}</div>
    </section>
  );
}

/** Une section en groupe nommé, pour les compteurs qui n'existent que si le Rapport les porte. */
function Groupe({ titre, children, className }: { titre: string; children: ReactNode; className?: string }) {
  return (
    <section role="group" aria-label={titre} className={cn("flex flex-col gap-3", className)}>
      <h3 className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">{titre}</h3>
      <div className="flex flex-wrap gap-x-4 gap-y-3">{children}</div>
    </section>
  );
}

const CAPACITES: [ToucheDeCapacite, keyof NonNullable<Details["sortsLances"]>][] = [
  ["Q", "q"],
  ["W", "w"],
  ["E", "e"],
  ["R", "r"],
];

const PINGS: Ping[] = [
  "allIn",
  "assistMe",
  "basic",
  "command",
  "danger",
  "enemyMissing",
  "enemyVision",
  "getBack",
  "hold",
  "needVision",
  "onMyWay",
  "push",
  "retreat",
  "visionCleared",
];

/** Un sort lancé : son icône, sa touche en pastille et le nombre de lancers. */
function SortLance({ touche, nombre, icone }: { touche: string; nombre: number | null | undefined; icone: ReactNode }) {
  return (
    <div className="flex w-14 flex-col items-center gap-1 text-center">
      <span className="relative">
        {icone}
        <span className="absolute -bottom-2 left-1/2 flex h-4 min-w-4 -translate-x-1/2 items-center justify-center rounded-full border bg-background px-1 text-[10px] leading-none">
          {touche}
        </span>
      </span>
      <span className="mt-2 font-mono text-lg font-semibold tabular-nums">{statLabel(nombre ?? null)}</span>
    </div>
  );
}

/** Les dégâts d'un type : son nom, sa valeur en milliers et une barre en part des dégâts totaux. */
function BarreDeDegats({
  nom,
  valeur,
  total,
  className,
}: {
  nom: string;
  valeur: number | null;
  total: number;
  className: string;
}) {
  return (
    <div className="flex flex-col gap-1">
      <dl className="flex items-baseline justify-between text-sm">
        <dt className="text-muted-foreground">{nom}</dt>
        <dd className="font-mono font-semibold tabular-nums">{millierLabel(valeur)}</dd>
      </dl>
      <progress
        aria-hidden
        value={valeur ?? 0}
        max={total || 1}
        className={cn(
          "h-2 w-full appearance-none overflow-hidden rounded-full [&::-moz-progress-bar]:bg-current [&::-webkit-progress-bar]:bg-muted [&::-webkit-progress-value]:bg-current",
          className,
        )}
      />
    </div>
  );
}

/**
 * Le détail d'un joueur de la game : vision, stats par minute, dégâts par type, sorts lancés et pings
 * (ces deux derniers seulement si le Rapport les porte ; les pings à zéro ne sont pas affichés).
 */
export function DetailsDuJoueur({ participant, side }: { participant: Participant; side: "blue" | "red" }) {
  const champions = useChampions();
  const champion = champions.data?.find((c) => c.id === participant.championId);
  const s = participant.stats;
  const d = participant.details;
  const role = posteLabel(d?.role ?? s?.position);
  const sorts = d?.sortsLances;
  const pings = d?.pings ? PINGS.filter((cle) => (d.pings?.[cle] ?? 0) > 0) : [];
  const physiques = s?.physicalDamageToChampions ?? null;
  const magiques = s?.magicDamageToChampions ?? null;
  const bruts = s?.trueDamageToChampions ?? null;
  const total = (physiques ?? 0) + (magiques ?? 0) + (bruts ?? 0);

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
      <CardContent className="grid gap-8 md:grid-cols-3">
        <Section titre="Vision" className="grid-cols-3">
          <Mesure
            nom="Balises posées"
            valeur={statLabel(s?.wardsPlaced ?? null)}
            icone={
              <Pastille className="bg-indigo-400/15 text-indigo-300">
                <Eye />
              </Pastille>
            }
          />
          <Mesure
            nom="Balises détruites"
            valeur={statLabel(s?.wardsKilled ?? null)}
            icone={
              <Pastille className="bg-red-400/15 text-red-400">
                <EyeOff />
              </Pastille>
            }
          />
          <Mesure
            nom="Balises de contrôle"
            valeur={statLabel(s?.controlWardsBought ?? null)}
            icone={
              <Pastille className="bg-amber-400/15 text-amber-400">
                <Shield />
              </Pastille>
            }
          />
        </Section>
        <Section titre="Par minute" className="grid-cols-4">
          <Mesure
            nom="CS par minute"
            valeur={nombreLabel(d?.csParMinute ?? null, 1)}
            icone={
              <Pastille className="bg-amber-400/15 text-amber-400">
                <Sword />
              </Pastille>
            }
          />
          <Mesure
            nom="Vision par minute"
            valeur={nombreLabel(d?.visionParMinute ?? null, 1)}
            icone={
              <Pastille className="bg-indigo-400/15 text-indigo-300">
                <Eye />
              </Pastille>
            }
          />
          <Mesure
            nom="Dégâts par minute"
            valeur={nombreLabel(d?.degatsParMinute ?? null)}
            icone={
              <Pastille className="bg-red-400/15 text-red-400">
                <Flame />
              </Pastille>
            }
          />
          <Mesure
            nom="Or par minute"
            valeur={nombreLabel(d?.orParMinute ?? null)}
            icone={
              <Pastille className="bg-emerald-400/15 text-emerald-400">
                <Coins />
              </Pastille>
            }
          />
        </Section>
        <Section titre="Dégâts par type">
          <BarreDeDegats nom="Dégâts physiques" valeur={physiques} total={total} className="text-orange-400" />
          <BarreDeDegats nom="Dégâts magiques" valeur={magiques} total={total} className="text-sky-400" />
          <BarreDeDegats nom="Dégâts bruts" valeur={bruts} total={total} className="text-zinc-200" />
        </Section>
        {sorts && (
          <Groupe titre="Sorts lancés" className="md:col-span-2">
            {CAPACITES.map(([touche, cle]) => (
              <SortLance
                key={cle}
                touche={touche}
                nombre={sorts[cle]}
                icone={<IconeCapacite championId={participant.championId} touche={touche} className="size-12" />}
              />
            ))}
            <SortLance touche="D" nombre={sorts.d} icone={<IconeSort sortId={d?.spell1Id} className="size-12" />} />
            <SortLance touche="F" nombre={sorts.f} icone={<IconeSort sortId={d?.spell2Id} className="size-12" />} />
          </Groupe>
        )}
        {pings.length > 0 && (
          <Groupe titre="Pings">
            {pings.map((cle) => (
              <span key={cle} className="flex flex-col items-center gap-1">
                <IconePing ping={cle} className="size-6" />
                <span className="font-mono text-base font-semibold tabular-nums">{statLabel(d?.pings?.[cle] ?? null)}</span>
              </span>
            ))}
          </Groupe>
        )}
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
