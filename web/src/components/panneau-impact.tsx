import type { RefObject } from "react";
import type { Schemas } from "@/api/client";
import { AnneauDeNote } from "@/components/icones-de-jeu/anneau-de-note";
import { IconeChampion } from "@/components/icones-de-jeu/icones-catalogue";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { nombreLabel, partLabel, posteLabel } from "@/lib/game";
import { cn } from "@/lib/utils";

type Participant = Schemas["GameHistoryParticipantDto"];
type Impact = Schemas["ImpactDuJoueur"];
type Groupe = Schemas["GroupeDImpact"];
type Critere = Schemas["CritereDeNote"];
type Axe = Schemas["AxeDImpact"];

const ORDRE: Groupe[] = ["Global", "VsAdversaire", "Objectifs", "Equipe", "Role"];

const NOMS_DE_GROUPE: Record<Exclude<Groupe, "Role">, string> = {
  Global: "Global",
  VsAdversaire: "Vs adversaire",
  Objectifs: "Objectifs",
  Equipe: "Équipe",
};

type Format = "decimal1" | "entier" | "part" | "oui-non";

const CRITERES: Record<Critere, { nom: string; format: Format }> = {
  Kda: { nom: "KDA", format: "decimal1" },
  ParticipationAuxKills: { nom: "Participation aux kills", format: "part" },
  CsParMinute: { nom: "CS par minute", format: "decimal1" },
  DegatsParMinute: { nom: "Dégâts par minute", format: "entier" },
  VisionParMinute: { nom: "Vision par minute", format: "decimal1" },
  ObjectifsParMinute: { nom: "Dégâts aux objectifs par minute", format: "entier" },
  OrParMinute: { nom: "Or par minute", format: "entier" },
  PartDesKills: { nom: "Part des kills de l'équipe", format: "part" },
  PartDesDegats: { nom: "Part des dégâts de l'équipe", format: "part" },
  PartDeLOr: { nom: "Part de l'or de l'équipe", format: "part" },
  Victoire: { nom: "Victoire", format: "oui-non" },
};

function valeurLabel(valeur: number, format: Format): string {
  switch (format) {
    case "decimal1":
      return nombreLabel(valeur, 1);
    case "entier":
      return nombreLabel(valeur, 0);
    case "part":
      return partLabel(valeur, 1);
    case "oui-non":
      return valeur >= 0.5 ? "oui" : "non";
  }
}

const pointsLabel = (points: number): string => `+${nombreLabel(points, 1)}`;

const COULEUR_DE_LETTRE: Record<string, string> = {
  "S+": "bg-amber-400/20 text-amber-400",
  S: "bg-amber-400/20 text-amber-400",
  A: "bg-emerald-500/20 text-emerald-400",
  B: "bg-sky-500/20 text-sky-400",
  C: "bg-orange-500/20 text-orange-400",
  D: "bg-red-500/20 text-red-400",
};

const couleurDeScore = (score: number): string =>
  score >= 70 ? "text-emerald-400" : score >= 45 ? "text-amber-400" : "text-red-400";

const NOMS_DES_AXES: Record<Axe, string> = {
  Survie: "Survie",
  Utilite: "Utilité",
  Combat: "Combat",
  Objectifs: "Objectifs",
  Vision: "Vision",
  Economie: "Économie",
};

/** Les mesures de chaque axe, telles que les décrit l'ADR 0011. */
const ORIGINE_DES_AXES: Array<[Axe, string]> = [
  ["Survie", "morts par minute (moins vaut mieux)."],
  ["Utilite", "soins et boucliers posés par minute, secondes de contrôle infligé par minute."],
  ["Combat", "KDA, dégâts par minute, part des dégâts de l'équipe."],
  ["Objectifs", "dégâts aux objectifs par minute, tourelles, dragons et barons pris."],
  ["Vision", "score de vision par minute, balises posées et détruites par minute."],
  ["Economie", "or par minute, CS par minute (pas pour le Support)."],
];

/**
 * Le panneau Impact d'un joueur : sa note, les critères de la note par groupe (valeur, score, points)
 * et son face-à-face à six axes contre l'adversaire du même rôle. Ne montre que ce que l'API donne.
 */
export function PanneauImpact({
  joueur,
  impact,
  adversaire,
  equipe,
  victoire,
  onFermer,
  retour,
}: {
  joueur: Participant;
  impact: Impact;
  adversaire: Participant | null;
  equipe: string;
  victoire: boolean;
  onFermer: () => void;
  /** L'élément qui reprend le focus à la fermeture (la ligne cliquée). */
  retour: RefObject<HTMLElement | null>;
}) {
  const role = posteLabel(joueur.performance?.role ?? joueur.details?.role ?? joueur.stats?.position);
  const groupes = ORDRE.flatMap((g) => impact.sousScores.filter((s) => s.groupe === g));
  const global = groupes.find((g) => g.groupe === "Global");
  const nomDuGroupe = (g: Groupe) => (g === "Role" ? (role ?? "Rôle") : NOMS_DE_GROUPE[g]);
  const description = [role, victoire ? "Victoire" : "Défaite", `Équipe ${equipe}`].filter(Boolean).join(" · ");

  return (
    <Dialog open onOpenChange={(ouvert) => !ouvert && onFermer()}>
      <DialogContent
        className="sm:max-w-2xl"
        onCloseAutoFocus={(e) => {
          e.preventDefault();
          retour.current?.focus();
        }}
      >
        <DialogHeader className="flex-row items-center gap-3 pr-8">
          <IconeChampion championId={joueur.championId} className="size-12 rounded-lg border-2" />
          <div className="flex min-w-0 flex-col gap-1">
            <DialogTitle className="truncate text-lg">{joueur.prenom}</DialogTitle>
            <DialogDescription>{description}</DialogDescription>
          </div>
          {joueur.performance && (
            <div className="ml-auto flex flex-col items-center gap-0.5">
              <AnneauDeNote note={joueur.performance.note} className="size-14" />
              {global && <span className="text-[11px] text-muted-foreground">Note · {global.lettre}</span>}
            </div>
          )}
        </DialogHeader>

        {groupes.length === 0 ? (
          <p className="text-sm text-muted-foreground">Aucun critère mesuré pour ce joueur dans cette game.</p>
        ) : (
          <Tabs defaultValue={groupes[0]!.groupe}>
            <TabsList className="w-full">
              {groupes.map((g) => (
                <TabsTrigger key={g.groupe} value={g.groupe}>
                  {nomDuGroupe(g.groupe)}
                </TabsTrigger>
              ))}
            </TabsList>
            {groupes.map((g) => (
              <TabsContent key={g.groupe} value={g.groupe} className="flex flex-col gap-3">
                <div className="flex items-center gap-3">
                  <span
                    role="img"
                    aria-label={`Lettre ${g.lettre}`}
                    className={cn(
                      "flex size-9 items-center justify-center rounded-lg font-bold",
                      COULEUR_DE_LETTRE[g.lettre],
                    )}
                  >
                    {g.lettre}
                  </span>
                  <span className="text-sm font-semibold">{nomDuGroupe(g.groupe)}</span>
                  <span className="ml-auto text-sm text-muted-foreground">{pointsLabel(g.points)} points</span>
                </div>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Critère</TableHead>
                      <TableHead className="text-right">Valeur</TableHead>
                      <TableHead className="text-right">Score</TableHead>
                      <TableHead className="text-right">Points</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {g.lignes.map((l) => (
                      <TableRow key={l.critere}>
                        <TableCell>{CRITERES[l.critere].nom}</TableCell>
                        <TableCell className="text-right font-mono tabular-nums">
                          {valeurLabel(l.valeur, CRITERES[l.critere].format)}
                        </TableCell>
                        <TableCell className={cn("text-right font-mono tabular-nums", couleurDeScore(l.score))}>
                          {l.score}
                        </TableCell>
                        <TableCell className="text-right font-mono font-semibold tabular-nums">
                          {pointsLabel(l.points)}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </TabsContent>
            ))}
          </Tabs>
        )}

        {impact.faceAFace && (
          <FaceAFace joueur={joueur} adversaire={adversaire} face={impact.faceAFace} role={role} />
        )}
      </DialogContent>
    </Dialog>
  );
}

function FaceAFace({
  joueur,
  adversaire,
  face,
  role,
}: {
  joueur: Participant;
  adversaire: Participant | null;
  face: NonNullable<Impact["faceAFace"]>;
  role: string | null;
}) {
  const nomAdversaire = adversaire?.prenom ?? "l'adversaire";
  const roleDeLAdversaire = posteLabel(face.role) ?? role;
  const mesures = new Set(face.axes.map((a) => a.axe));
  return (
    <section aria-label="Face-à-face" className="flex flex-col gap-3 rounded-xl border bg-muted/20 p-4">
      <header className="flex flex-wrap items-center gap-2">
        <h3 className="text-base font-semibold">Face-à-face</h3>
        <span className="text-sm text-muted-foreground">
          {`${joueur.prenom}${role ? ` (${role})` : ""} contre ${nomAdversaire}${roleDeLAdversaire ? ` (${roleDeLAdversaire})` : ""}`}
        </span>
        <span className="ml-auto rounded-full bg-muted px-2.5 py-0.5 text-xs font-bold">
          {`${face.axesGagnes} – ${face.axesPerdus}`}
        </span>
      </header>
      <ul className="flex flex-col gap-2">
        {face.axes.map((a) => (
          <li
            key={a.axe}
            aria-label={NOMS_DES_AXES[a.axe]}
            className="grid grid-cols-[5.5rem_minmax(0,1fr)_6rem] items-center gap-3 text-sm"
          >
            <span>{NOMS_DES_AXES[a.axe]}</span>
            <div className="flex flex-col gap-1">
              <progress
                aria-label={`${joueur.prenom} : ${a.score}`}
                value={a.score}
                max={100}
                className="h-2 w-full appearance-none overflow-hidden rounded-full bg-muted [&::-moz-progress-bar]:bg-sky-400 [&::-webkit-progress-bar]:bg-muted [&::-webkit-progress-value]:bg-sky-400"
              />
              <progress
                aria-label={`${nomAdversaire} : ${a.scoreAdversaire}`}
                value={a.scoreAdversaire}
                max={100}
                className="h-1 w-full appearance-none overflow-hidden rounded-full bg-muted [&::-moz-progress-bar]:bg-red-400 [&::-webkit-progress-bar]:bg-muted [&::-webkit-progress-value]:bg-red-400"
              />
            </div>
            <span className={cn("text-right font-semibold", a.ecart >= 0 ? "text-emerald-400" : "text-red-400")}>
              {a.score} <span className="text-xs">({a.ecart > 0 ? `+${a.ecart}` : a.ecart})</span>
            </span>
          </li>
        ))}
      </ul>
      <p className="text-xs text-muted-foreground">
        Barre bleue : le joueur. Barre rouge : l'adversaire du même rôle.
      </p>
      <div className="flex flex-col gap-1 border-t pt-3">
        <h4 className="text-xs font-semibold tracking-wide text-muted-foreground uppercase">
          D'où viennent les axes
        </h4>
        {ORIGINE_DES_AXES.filter(([axe]) => mesures.has(axe)).map(([axe, texte]) => (
          <p key={axe} className="text-xs text-muted-foreground">
            <span className="font-semibold text-foreground">{NOMS_DES_AXES[axe]}</span> : {texte}
          </p>
        ))}
      </div>
    </section>
  );
}
