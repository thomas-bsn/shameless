import { useQueryClient } from "@tanstack/react-query";
import { MinusIcon, PlusIcon } from "lucide-react";
import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { isMasterPlus, rankLabel, SOLOQ_TIERS, tierName } from "@/lib/ranks";

const COST_MIN = 1;
const COST_MAX = 6;
const arrondi = (cost: number) => Math.round(cost * 10) / 10;
const formatCost = (cost: number) => cost.toLocaleString("fr-FR", { minimumFractionDigits: 1, maximumFractionDigits: 1 });
const classeSelect = "h-9 rounded-md border bg-background px-2 text-sm";

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
      <Input aria-label="Cost de départ" readOnly value={formatCost(valeur)} className="h-8 w-16 text-center font-mono" />
      <Button variant="outline" size="icon-sm" aria-label="Augmenter le cost" disabled={valeur >= COST_MAX} onClick={() => regler(0.1)}>
        <PlusIcon />
      </Button>
      {modifie && (
        <>
          <span className="text-xs text-muted-foreground">{formatCost(depart)} avant</span>
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
  const [tier, setTier] = useState("DIAMOND");
  const [division, setDivision] = useState(1);
  const [lp, setLp] = useState(0);
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
    <div className="flex flex-wrap items-center gap-2">
      <select aria-label="Tier du peak" className={classeSelect} value={tier} onChange={(e) => setTier(e.target.value)}>
        {SOLOQ_TIERS.filter((t) => t !== "UNRANKED").map((t) => (
          <option key={t} value={t}>
            {tierName(t)}
          </option>
        ))}
      </select>
      <select
        aria-label="Division du peak"
        className={classeSelect}
        value={division}
        disabled={isMasterPlus(tier)}
        onChange={(e) => setDivision(Number(e.target.value))}
      >
        {[1, 2, 3, 4].map((d) => (
          <option key={d} value={d}>
            {["I", "II", "III", "IV"][d - 1]}
          </option>
        ))}
      </select>
      <Input aria-label="LP du peak" type="number" min={0} className="h-9 w-20" value={lp} onChange={(e) => setLp(Number(e.target.value))} />
      <Button
        size="sm"
        disabled={renseigner.isPending}
        onClick={() =>
          renseigner.mutate({
            params: { path },
            body: { peakTier: tier, peakDivision: isMasterPlus(tier) ? null : division, peakSeason: null, peakLp: lp },
          })
        }
      >
        Enregistrer le peak
      </Button>
      <Button variant="ghost" size="sm" onClick={() => setSaisie(false)}>
        Annuler
      </Button>
      {renseigner.isError && <ErrorAlert error={renseigner.error} />}
    </div>
  );
}

/** L'onglet Joueurs : cost de départ (− et +, enregistré par ligne), source et peak. */
export function JoueursAdmin() {
  const players = $api.useQuery("get", "/players");
  if (players.isPending) return <LoadingRows />;
  if (players.isError) return <ErrorAlert error={players.error} />;
  if (players.data.length === 0) return <p className="text-sm text-muted-foreground">Aucun joueur.</p>;

  return (
    <div className="overflow-x-auto rounded-xl border bg-card">
      <table className="w-full min-w-[680px] text-sm">
        <thead>
          <tr className="border-b text-left text-xs tracking-wide text-muted-foreground uppercase">
            <th className="p-3 font-medium">Joueur</th>
            <th className="p-3 font-medium">Cost de départ</th>
            <th className="p-3 font-medium">Source</th>
            <th className="p-3 font-medium">Peak</th>
          </tr>
        </thead>
        <tbody>
          {players.data.map((player) => (
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
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
