import { Undo2Icon } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { type ChampCorrigeable, VOIES } from "@/lib/champs-corrigeables";
import { cn } from "@/lib/utils";
import type { EntreeDuCatalogue } from "./use-catalogue";

const CHAMP_NATIF = "h-8 w-full min-w-0 rounded-md border border-input bg-background px-2 text-sm shadow-xs";

interface Props {
  champ: ChampCorrigeable;
  prenom: string;
  valeur: string;
  /** La valeur du Companion ; null s'il n'a rien envoyé. */
  origine: string | null;
  /** Une correction est enregistrée sur ce champ. */
  corrige: boolean;
  /** Les choix du catalogue pour cette case ; null : un champ d'identifiant numérique. */
  entrees: EntreeDuCatalogue[] | null;
  onChange: (valeur: string) => void;
  onRetablir: () => void;
  occupe: boolean;
}

/** Le nom d'une valeur : celui du catalogue quand il est connu, sinon l'identifiant. */
const nomDe = (entrees: EntreeDuCatalogue[] | null, valeur: string): string => {
  if (valeur === "") return "–";
  if (valeur === "0" && entrees) return "vide";
  return entrees?.find((e) => String(e.id) === valeur)?.name ?? valeur;
};

/** Une case de stat : la valeur affichée, celle du Companion dessous, et le retour à cette dernière. */
export function CaseDeStat({ champ, prenom, valeur, origine, corrige, entrees, onChange, onRetablir, occupe }: Props) {
  const id = `stat-${champ.nom}`;
  const nom = `${champ.label} de ${prenom}`;
  const modifie = corrige || (origine !== null && origine !== valeur);
  const entree = entrees?.find((e) => String(e.id) === valeur);

  return (
    <div data-champ={champ.nom} data-modifie={modifie ? "true" : "false"} className="flex flex-col gap-1">
      <div className="flex items-center justify-between gap-1">
        <Label htmlFor={id} className="text-xs text-muted-foreground">
          {champ.label}
          <span className="sr-only"> de {prenom}</span>
        </Label>
        {modifie && (
          <span className="flex items-center gap-1">
            <span className="text-[11px] text-amber-500">{corrige ? "corrigé" : "modifié"}</span>
            <button
              type="button"
              aria-label={`Rétablir ${nom}`}
              disabled={occupe}
              className="rounded p-0.5 text-muted-foreground hover:text-foreground disabled:opacity-50"
              onClick={onRetablir}
            >
              <Undo2Icon className="size-3.5" />
            </button>
          </span>
        )}
      </div>
      <div className={cn("flex items-center gap-2 rounded-md", modifie && "ring-1 ring-amber-400")}>
        {entree && <img src={entree.iconUrl} alt="" className="size-7 shrink-0 rounded" />}
        <Controle id={id} champ={champ} valeur={valeur} entrees={entrees} onChange={onChange} />
      </div>
      <span className="text-[11px] text-muted-foreground">Companion : {origine === null ? "–" : nomDe(entrees, origine)}</span>
    </div>
  );
}

function Controle({ id, champ, valeur, entrees, onChange }: Pick<Props, "champ" | "valeur" | "entrees" | "onChange"> & { id: string }) {
  if (champ.type === "voie") {
    return (
      <select id={id} className={CHAMP_NATIF} value={valeur} onChange={(e) => onChange(e.target.value)}>
        <option value="">–</option>
        {VOIES.map((v) => (
          <option key={v} value={v}>
            {v}
          </option>
        ))}
      </select>
    );
  }
  if (entrees) {
    return (
      <select id={id} className={CHAMP_NATIF} value={valeur} onChange={(e) => onChange(e.target.value)}>
        {valeur === "" && <option value="">–</option>}
        <option value="0">Aucun</option>
        {valeur !== "" && valeur !== "0" && !entrees.some((e) => String(e.id) === valeur) && <option value={valeur}>#{valeur}</option>}
        {entrees.map((e) => (
          <option key={e.id} value={e.id}>
            {e.name}
          </option>
        ))}
      </select>
    );
  }
  const texte = champ.type === "champion";
  return (
    <Input
      id={id}
      className="h-8 font-mono"
      type={texte ? "text" : "number"}
      min={texte ? undefined : 0}
      value={valeur}
      onChange={(e) => onChange(e.target.value)}
    />
  );
}
