import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { grouperLesEcarts } from "@/lib/ecarts-import";
import { Pied, type Retour } from "./pied";
import { useRelire } from "./use-relire";

/** Les écarts d'un import refusé (409) : la liste des différences entre le fichier et la game. */
const ecartsDe = (erreur: unknown): string[] | null => {
  const ecarts = (erreur as { ecarts?: unknown } | null)?.ecarts;
  return Array.isArray(ecarts) ? ecarts.map(String) : null;
};

const resume = (noms: string[]) => (noms.length > 3 ? `${noms.slice(0, 3).join(", ")}, +${noms.length - 3}` : noms.join(", "));

/**
 * L'Import d'un Rapport (ADR 0020) : le Fichier de Rapport remis par un joueur dont le Companion n'a pas
 * pu envoyer. Il doit correspondre à la game (participants, Sides, vainqueur), sinon il est refusé en
 * entier et les écarts s'affichent, regroupés.
 */
export function OngletRapport({ gameId, onFermer }: { gameId: number; onFermer: () => void }) {
  const relire = useRelire();
  const [fichier, setFichier] = useState<File | null>(null);
  const [retour, setRetour] = useState<Retour>(null);
  const [ecarts, setEcarts] = useState<string[] | null>(null);
  const [detail, setDetail] = useState(false);
  const importer = $api.useMutation("post", "/games/{id}/import-rapport", {
    onError: (erreur) => {
      const liste = ecartsDe(erreur);
      if (liste) setEcarts(liste);
      else setRetour({ type: "erreur", erreur });
    },
    onSuccess: async () => {
      setRetour({ type: "ok", texte: "Rapport importé." });
      await relire();
    },
  });

  const lancer = async () => {
    if (!fichier) return;
    setRetour(null);
    setEcarts(null);
    setDetail(false);
    let rapport: unknown;
    try {
      rapport = JSON.parse(await fichier.text());
    } catch {
      setRetour({ type: "erreur", erreur: { status: 400, detail: "Ce fichier n'est pas un fichier JSON : déposez le Fichier de Rapport tel que reçu." } });
      return;
    }
    importer.mutate({ params: { path: { id: gameId } }, body: rapport as Schemas["RapportDeGameRequest"] });
  };

  const { groupes, autres } = grouperLesEcarts(ecarts ?? []);

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm text-muted-foreground">
        Déposez le Fichier de Rapport d'un joueur dont le Companion n'a pas pu envoyer. Ses participants, ses Sides et son vainqueur doivent
        être ceux de la game : le vainqueur et la composition ne changent jamais, les stats, les champions et les costs si.
      </p>
      <div className="flex flex-wrap items-end gap-3 rounded-lg border border-dashed p-3">
        <div className="flex min-w-0 flex-1 flex-col gap-1">
          <Label htmlFor={`rapport-${gameId}`} className="text-xs">
            Fichier de Rapport
          </Label>
          <Input id={`rapport-${gameId}`} type="file" accept=".json,application/json" onChange={(e) => setFichier(e.target.files?.[0] ?? null)} />
        </div>
        <Button disabled={!fichier || importer.isPending} onClick={() => void lancer()}>
          Importer le rapport
        </Button>
      </div>

      {ecarts && (
        <div className="flex flex-col gap-3 rounded-lg border border-destructive/40 bg-destructive/5 p-3">
          <p className="text-sm font-semibold text-destructive">Import refusé : rien n'a été importé</p>
          <p className="text-xs">Le fichier ne correspond pas à la game. {ecarts.length} écart{ecarts.length > 1 ? "s" : ""}, regroupés :</p>
          <ul className="flex flex-col gap-1.5">
            {groupes.map((g) => (
              <li key={g.titre} className={`flex justify-between gap-3 rounded-md bg-background px-3 py-2 text-sm ${g.nombre === 0 ? "text-muted-foreground" : ""}`}>
                <span>{g.titre}</span>
                <span className="text-xs text-muted-foreground">
                  {g.titre === "Vainqueur différent" ? (g.nombre > 0 ? "oui" : "non") : g.nombre === 0 ? "0" : `${g.nombre} · ${resume(g.noms)}`}
                </span>
              </li>
            ))}
            {autres.length > 0 && (
              <li className="flex justify-between gap-3 rounded-md bg-background px-3 py-2 text-sm">
                <span>Autres écarts</span>
                <span className="text-xs text-muted-foreground">{autres.length}</span>
              </li>
            )}
          </ul>
          <Button variant="outline" size="sm" className="self-start" onClick={() => setDetail((d) => !d)}>
            {detail ? "Masquer les écarts" : `Voir les ${ecarts.length} écarts`}
          </Button>
          {detail && (
            <ul className="list-disc pl-5 text-sm">
              {ecarts.map((e) => (
                <li key={e}>{e}</li>
              ))}
            </ul>
          )}
        </div>
      )}

      <Pied retour={retour}>
        <Button variant="outline" onClick={onFermer}>
          Fermer
        </Button>
      </Pied>
    </div>
  );
}
