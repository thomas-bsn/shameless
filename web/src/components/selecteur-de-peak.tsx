import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { isMasterPlus, SOLOQ_TIERS, tierName } from "@/lib/ranks";

export type Peak = Pick<Schemas["UpdatePeakRequest"], "peakTier" | "peakDivision" | "peakSeason" | "peakLp">;

const classeSelect = "h-9 rounded-md border bg-background px-2 text-sm";

/**
 * Choix d'un peak : rang (tier, division, LP) et saison, de S24 à la saison courante (l'API fournit la liste).
 * Aucune vérification du rang. Réutilisé par l'Administration et par le Profil.
 */
export function SelecteurDePeak({
  initial,
  onValider,
  onAnnuler,
  enCours = false,
  erreur,
}: {
  initial?: Peak | null;
  onValider: (peak: Peak) => void;
  onAnnuler?: () => void;
  enCours?: boolean;
  erreur?: unknown;
}) {
  const saisons = $api.useQuery("get", "/players/peak/saisons");
  const [tier, setTier] = useState(initial?.peakTier && initial.peakTier !== "UNRANKED" ? initial.peakTier : "DIAMOND");
  const [division, setDivision] = useState(initial?.peakDivision ?? 1);
  const [lp, setLp] = useState(initial?.peakLp ?? 0);
  const [saisonChoisie, setSaisonChoisie] = useState<string | null>(initial?.peakSeason ?? null);

  if (saisons.isError) return <ErrorAlert error={saisons.error} />;
  if (saisons.isPending) return <span className="text-muted-foreground">…</span>;

  const saison = saisonChoisie ?? saisons.data.courante;

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
      <select aria-label="Saison du peak" className={classeSelect} value={saison} onChange={(e) => setSaisonChoisie(e.target.value)}>
        {saisons.data.saisons.map((s) => (
          <option key={s} value={s}>
            {s}
          </option>
        ))}
      </select>
      <Button
        size="sm"
        disabled={enCours}
        onClick={() =>
          onValider({ peakTier: tier, peakDivision: isMasterPlus(tier) ? null : division, peakSeason: saison, peakLp: lp })
        }
      >
        Enregistrer le peak
      </Button>
      {onAnnuler && (
        <Button variant="ghost" size="sm" onClick={onAnnuler}>
          Annuler
        </Button>
      )}
      {erreur != null && <ErrorAlert error={erreur} />}
    </div>
  );
}
