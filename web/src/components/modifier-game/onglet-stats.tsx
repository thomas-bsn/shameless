import { ChevronLeft, ChevronRight } from "lucide-react";
import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { CHAMPS, corpsDeCorrection, normaliser, SECTIONS, valeurCourante, valeurOrigine } from "@/lib/champs-corrigeables";
import { kdaLabel, posteLabel, trierParRole } from "@/lib/game";
import { CaseDeStat } from "./case-de-stat";
import { Pied, type Retour } from "./pied";
import { entreesDe, useCatalogue } from "./use-catalogue";
import { useRelire } from "./use-relire";

type Recap = Schemas["GameRecapResponse"];
type Participant = Recap["teamA"][number];
type Brouillons = Record<number, Record<string, string>>;

const idDeSection = (section: string) => `section-${normaliser(section).replace(/\W+/g, "-")}`;

/** Les stats des dix joueurs : la liste, puis toutes les stats d'un joueur par sections. */
export function OngletStats({ game, onFermer }: { game: Recap; onFermer: () => void }) {
  const joueurs: { participant: Participant; equipe: "A" | "B" }[] = [
    ...trierParRole(game.teamA).map((participant) => ({ participant, equipe: "A" as const })),
    ...trierParRole(game.teamB).map((participant) => ({ participant, equipe: "B" as const })),
  ];
  const [courant, setCourant] = useState<number | null>(null);
  const [brouillons, setBrouillons] = useState<Brouillons>({});

  if (courant === null || !joueurs[courant]) {
    return <ListeDesJoueurs joueurs={joueurs} onChoisir={setCourant} onFermer={onFermer} />;
  }
  const { participant, equipe } = joueurs[courant];
  return (
    <StatsDUnJoueur
      key={participant.playerId}
      game={game}
      participant={participant}
      equipe={equipe}
      rang={courant}
      total={joueurs.length}
      brouillon={brouillons[participant.playerId] ?? {}}
      onBrouillon={(b) => setBrouillons((tous) => ({ ...tous, [participant.playerId]: b }))}
      onAller={setCourant}
    />
  );
}

function ListeDesJoueurs({
  joueurs,
  onChoisir,
  onFermer,
}: {
  joueurs: { participant: Participant; equipe: "A" | "B" }[];
  onChoisir: (rang: number) => void;
  onFermer: () => void;
}) {
  return (
    <div className="flex flex-col gap-4">
      <ul className="flex flex-col gap-1.5">
        {joueurs.map(({ participant: p, equipe }, rang) => (
          <li key={p.playerId}>
            <button
              type="button"
              aria-label={`Stats de ${p.prenom}`}
              onClick={() => onChoisir(rang)}
              className="flex w-full items-center gap-3 rounded-lg border px-3 py-2 text-left text-sm hover:bg-muted"
            >
              <span className="w-16 text-xs text-muted-foreground">Équipe {equipe}</span>
              <span className="flex-1 font-medium">{p.prenom}</span>
              <span className="text-muted-foreground">{[p.championId, posteLabel(p.stats?.position)].filter(Boolean).join(" · ")}</span>
              <span className="w-20 text-right font-mono text-xs">{p.stats ? kdaLabel(p.stats) : "–"}</span>
              <span className="w-20 text-right">{p.champsCorriges.length > 0 && <Badge variant="secondary">corrigée</Badge>}</span>
            </button>
          </li>
        ))}
      </ul>
      <Pied>
        <Button variant="outline" onClick={onFermer}>
          Fermer
        </Button>
      </Pied>
    </div>
  );
}

function StatsDUnJoueur({
  game,
  participant,
  equipe,
  rang,
  total,
  brouillon,
  onBrouillon,
  onAller,
}: {
  game: Recap;
  participant: Participant;
  equipe: "A" | "B";
  rang: number;
  total: number;
  brouillon: Record<string, string>;
  onBrouillon: (b: Record<string, string>) => void;
  onAller: (rang: number | null) => void;
}) {
  const relire = useRelire();
  const catalogue = useCatalogue();
  const [recherche, setRecherche] = useState("");
  const [retour, setRetour] = useState<Retour>(null);
  const [occupe, setOccupe] = useState(false);
  const path = { id: game.id, playerId: participant.playerId };
  const options = { onMutate: () => setRetour(null) };
  const corriger = $api.useMutation("put", "/games/{id}/players/{playerId}/corrections", options);
  const annuler = $api.useMutation("delete", "/games/{id}/players/{playerId}/corrections/{champ}", options);

  const valeur = (nom: string) => brouillon[nom] ?? valeurCourante(participant, nom);
  const corrige = (nom: string) => participant.champsCorriges.includes(nom);
  const modifie = (nom: string) => corrige(nom) || valeur(nom) !== valeurCourante(participant, nom);
  const corps = corpsDeCorrection(participant, brouillon);
  const nbModifies = CHAMPS.filter((c) => modifie(c.nom)).length;

  const sansBrouillon = (nom: string) => Object.fromEntries(Object.entries(brouillon).filter(([k]) => k !== nom));
  const echec = (erreur: unknown) => setRetour({ type: "erreur", erreur });

  const enregistrer = async () => {
    if (!corps) return;
    try {
      setOccupe(true);
      await corriger.mutateAsync({ params: { path }, body: corps });
      onBrouillon({});
      setRetour({ type: "ok", texte: "Enregistré." });
      await relire();
    } catch (erreur) {
      echec(erreur);
    } finally {
      setOccupe(false);
    }
  };

  const retablir = async (noms: string[]) => {
    try {
      setOccupe(true);
      for (const champ of noms.filter(corrige)) await annuler.mutateAsync({ params: { path: { ...path, champ } } });
      onBrouillon(Object.fromEntries(Object.entries(brouillon).filter(([k]) => !noms.includes(k))));
      await relire();
    } catch (erreur) {
      echec(erreur);
    } finally {
      setOccupe(false);
    }
  };

  const filtre = normaliser(recherche.trim());
  const visibles = CHAMPS.filter((c) => filtre === "" || normaliser(c.label).includes(filtre));
  const aller = (suivant: number) => {
    setRetour(null);
    onAller(suivant);
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <Button variant="outline" size="icon" aria-label="Joueur précédent" disabled={rang === 0} onClick={() => aller(rang - 1)}>
          <ChevronLeft />
        </Button>
        <div className="min-w-0 flex-1 leading-tight">
          <p className="font-semibold">
            {participant.prenom}{" "}
            <span className="text-sm font-normal text-muted-foreground">
              · {[participant.championId, posteLabel(participant.stats?.position)].filter(Boolean).join(" · ")} · Équipe {equipe}
            </span>
          </p>
          <p className="text-xs text-muted-foreground">
            Game #{game.id} · joueur {rang + 1} sur {total}
          </p>
        </div>
        <Button variant="outline" size="icon" aria-label="Joueur suivant" disabled={rang === total - 1} onClick={() => aller(rang + 1)}>
          <ChevronRight />
        </Button>
        <Button variant="outline" size="sm" onClick={() => onAller(null)}>
          Retour à la liste
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-[11rem_minmax(0,1fr)]">
        <nav aria-label="Sections" className="flex flex-col gap-1 text-sm">
          <Input
            type="search"
            aria-label="Chercher une stat"
            placeholder="Chercher une stat…"
            className="mb-2 h-8"
            value={recherche}
            onChange={(e) => setRecherche(e.target.value)}
          />
          {SECTIONS.map((section) => {
            const n = CHAMPS.filter((c) => c.section === section && modifie(c.nom)).length;
            return (
              <button
                key={section}
                type="button"
                className="flex items-center justify-between rounded-md px-2 py-1.5 text-left text-muted-foreground hover:bg-muted hover:text-foreground"
                onClick={() => document.getElementById(idDeSection(section))?.scrollIntoView?.({ block: "start" })}
              >
                {section}
                {n > 0 && <span className="text-xs text-amber-500">{n}</span>}
              </button>
            );
          })}
        </nav>

        <div className="flex max-h-[50vh] min-h-0 flex-col gap-5 overflow-y-auto pr-1">
          {visibles.length === 0 && <p className="text-sm text-muted-foreground">Aucune stat ne correspond.</p>}
          {SECTIONS.map((section) => {
            const champs = visibles.filter((c) => c.section === section);
            if (champs.length === 0) return null;
            return (
              <section key={section} id={idDeSection(section)} className="flex flex-col gap-2">
                <h3 className="border-t pt-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">{section}</h3>
                <div className="grid grid-cols-2 gap-x-4 gap-y-3 lg:grid-cols-4">
                  {champs.map((champ) => (
                    <CaseDeStat
                      key={champ.nom}
                      champ={champ}
                      prenom={participant.prenom}
                      valeur={valeur(champ.nom)}
                      origine={valeurOrigine(participant, champ.nom)}
                      corrige={corrige(champ.nom)}
                      entrees={entreesDe(catalogue, champ.type)}
                      occupe={occupe}
                      onChange={(v) => {
                        setRetour(null);
                        onBrouillon({ ...brouillon, [champ.nom]: v });
                      }}
                      onRetablir={() => (corrige(champ.nom) ? void retablir([champ.nom]) : onBrouillon(sansBrouillon(champ.nom)))}
                    />
                  ))}
                </div>
              </section>
            );
          })}
        </div>
      </div>

      <Pied info={nbModifies > 0 ? `${nbModifies} stat${nbModifies > 1 ? "s" : ""} modifiée${nbModifies > 1 ? "s" : ""} pour ce joueur` : undefined} retour={retour}>
        <Button variant="outline" disabled={nbModifies === 0 || occupe} onClick={() => void retablir([...CHAMPS.map((c) => c.nom), ...participant.champsCorriges])}>
          Rétablir les valeurs du Companion
        </Button>
        <Button disabled={!corps || occupe} onClick={() => void enregistrer()}>
          Enregistrer ce joueur
        </Button>
      </Pied>
    </div>
  );
}
