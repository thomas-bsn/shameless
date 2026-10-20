import { useQueryClient } from "@tanstack/react-query";
import { MinusIcon, PlusIcon } from "lucide-react";
import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { ActualisationRiotPanel, BoutonActualiserJoueur } from "@/components/administration/actualisation-riot";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { SelecteurDePeak } from "@/components/selecteur-de-peak";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { costText } from "@/lib/cost";
import { rankLabel } from "@/lib/ranks";

const COST_MIN = 1;
const COST_MAX = 6;
const arrondi = (cost: number) => Math.round(cost * 10) / 10;

function CostDeDepart({ player }: { player: Schemas["PlayerDto"] }) {
  const queryClient = useQueryClient();
  const path = { playerId: player.id };
  const cost = $api.useQuery("get", "/players/{playerId}/cost", { params: { path } });
  const [brouillon, setBrouillon] = useState<number | null>(null);
  const enregistrer = $api.useMutation("put", "/players/{playerId}/cost", {
    onSuccess: async () => {
      setBrouillon(null);
      await queryClient.invalidateQueries({ queryKey: ["get", "/players/{playerId}/cost"] });
    },
  });

  if (cost.isError) return <ErrorAlert error={cost.error} />;
  if (cost.isPending) return <span className="text-muted-foreground">…</span>;

  const depart = cost.data.costDeDepart;
  const valeur = brouillon ?? depart;
  const modifie = brouillon !== null && brouillon !== depart;
  const regler = (delta: number) => setBrouillon(arrondi(valeur + delta));

  return (
    <div className="flex flex-wrap items-center gap-2">
      <Button variant="outline" size="icon-sm" aria-label="Diminuer le cost" disabled={valeur <= COST_MIN} onClick={() => regler(-0.1)}>
        <MinusIcon />
      </Button>
      <Input aria-label="Cost de départ" readOnly value={costText(valeur)} className="h-8 w-16 text-center font-mono" />
      <Button variant="outline" size="icon-sm" aria-label="Augmenter le cost" disabled={valeur >= COST_MAX} onClick={() => regler(0.1)}>
        <PlusIcon />
      </Button>
      {modifie && (
        <>
          <span className="text-xs text-muted-foreground">{costText(depart)} avant</span>
          <Button size="sm" disabled={enregistrer.isPending} onClick={() => enregistrer.mutate({ params: { path }, body: { cost: valeur } })}>
            Enregistrer
          </Button>
          <p className="w-full text-xs text-muted-foreground">Un cost fixé n'est plus écrasé par la game suivante (ADR 0012).</p>
        </>
      )}
      {enregistrer.isError && <ErrorAlert error={enregistrer.error} />}
    </div>
  );
}

function SourceDuCost({ playerId }: { playerId: number }) {
  const cost = $api.useQuery("get", "/players/{playerId}/cost", { params: { path: { playerId } } });
  if (!cost.data) return null;
  return cost.data.sourceDuCostDeDepart === "FixeParAdmin" ? (
    <Badge variant="outline" className="text-amber-500">
      fixé par un admin
    </Badge>
  ) : (
    <Badge variant="secondary">calculé</Badge>
  );
}

const aUnPeak = (p: Schemas["PlayerDto"]) => p.peakTier !== null && p.peakTier !== "UNRANKED";

function Peak({ player }: { player: Schemas["PlayerDto"] }) {
  const queryClient = useQueryClient();
  const path = { playerId: player.id };
  const rafraichir = () => queryClient.invalidateQueries({ queryKey: ["get", "/players"] });
  const [saisie, setSaisie] = useState(false);
  const renseigner = $api.useMutation("post", "/players/{playerId}/peak", {
    onSuccess: async () => {
      setSaisie(false);
      await rafraichir();
    },
  });
  const effacer = $api.useMutation("delete", "/players/{playerId}/peak", { onSuccess: rafraichir });

  if (aUnPeak(player)) {
    return (
      <div className="flex flex-wrap items-center gap-2">
        <span>{rankLabel(player.peakTier, player.peakDivision, player.peakLp)}</span>
        {player.peakSeason && <Badge variant="outline">{player.peakSeason}</Badge>}
        <Button variant="ghost" size="xs" disabled={effacer.isPending} onClick={() => effacer.mutate({ params: { path } })}>
          Effacer le peak
        </Button>
        {effacer.isError && <ErrorAlert error={effacer.error} />}
      </div>
    );
  }
  if (!saisie) {
    return (
      <Button variant="outline" size="xs" aria-label="Renseigner le peak" onClick={() => setSaisie(true)}>
        + Renseigner
      </Button>
    );
  }
  return (
    <SelecteurDePeak
      enCours={renseigner.isPending}
      erreur={renseigner.error}
      onAnnuler={() => setSaisie(false)}
      onValider={(peak) => renseigner.mutate({ params: { path }, body: peak })}
    />
  );
}

/** L'onglet Joueurs : cost de départ (− et +, enregistré par ligne), source et peak. */
export function JoueursAdmin() {
  const players = $api.useQuery("get", "/players");
  const [recherche, setRecherche] = useState("");
  if (players.isPending) return <LoadingRows />;
  if (players.isError) return <ErrorAlert error={players.error} />;
  if (players.data.length === 0) return <p className="text-sm text-muted-foreground">Aucun joueur.</p>;

  const terme = recherche.trim().toLowerCase();
  const visibles = players.data.filter((p) => p.prenom.toLowerCase().includes(terme));

  return (
    <div className="flex flex-col gap-4">
      <ActualisationRiotPanel />
      <Input
        type="search"
        aria-label="Rechercher un joueur"
        placeholder="Rechercher par prénom"
        value={recherche}
        onChange={(e) => setRecherche(e.target.value)}
        className="max-w-sm"
      />
      {visibles.length === 0 ? (
        <p className="text-sm text-muted-foreground">Aucun joueur ne correspond.</p>
      ) : (
      <div className="overflow-x-auto rounded-xl border bg-card">
      <table className="w-full min-w-[820px] text-sm">
        <thead>
          <tr className="border-b text-left text-xs tracking-wide text-muted-foreground uppercase">
            <th className="p-3 font-medium">Joueur</th>
            <th className="p-3 font-medium">Cost de départ</th>
            <th className="p-3 font-medium">Source</th>
            <th className="p-3 font-medium">Peak</th>
            <th className="p-3 font-medium">Riot</th>
          </tr>
        </thead>
        <tbody>
          {visibles.map((player) => (
            <tr key={player.id} className="border-b last:border-0">
              <td className="p-3 font-medium">{player.prenom}</td>
              <td className="p-3">
                <CostDeDepart player={player} />
              </td>
              <td className="p-3">
                <SourceDuCost playerId={player.id} />
              </td>
              <td className="p-3">
                <Peak player={player} />
              </td>
              <td className="p-3">
                <BoutonActualiserJoueur playerId={player.id} prenom={player.prenom} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      </div>
      )}
    </div>
  );
}
