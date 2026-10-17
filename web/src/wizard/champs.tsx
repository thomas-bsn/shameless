import { Check, ChevronDown, Copy, Eye, EyeOff } from "lucide-react";
import { useId, useState, type ReactNode } from "react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { cn } from "@/lib/utils";

/** Champ de saisie ; un secret est masqué, avec un bouton pour l'afficher. L'aide se déplie sous le champ. */
export function Champ({
  label, value, onChange, onBlur, error, secret, placeholder, aide,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
  onBlur?: () => void;
  error?: string;
  secret?: boolean;
  placeholder?: string;
  aide?: ReactNode;
}) {
  const id = useId();
  const [visible, setVisible] = useState(false);
  const [aideOuverte, setAideOuverte] = useState(false);
  return (
    <div className="space-y-1.5">
      <Label htmlFor={id}>{label}</Label>
      <div className="relative">
        <Input
          id={id}
          type={secret && !visible ? "password" : "text"}
          autoComplete="off"
          spellCheck={false}
          value={value}
          placeholder={placeholder}
          aria-invalid={!!error}
          aria-describedby={error ? `${id}-erreur` : undefined}
          onChange={(e) => onChange(e.target.value)}
          onBlur={onBlur}
          className={cn("font-mono", secret && "pr-10")}
        />
        {secret && (
          <button
            type="button"
            onClick={() => setVisible((v) => !v)}
            className="absolute inset-y-0 right-0 flex w-10 items-center justify-center text-muted-foreground hover:text-foreground"
            aria-label={`${visible ? "Masquer" : "Afficher"} le ${label}`}
          >
            {visible ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
          </button>
        )}
      </div>
      {error && <p id={`${id}-erreur`} className="text-xs text-destructive">{error}</p>}
      {aide && (
        <div>
          <button
            type="button"
            aria-expanded={aideOuverte}
            onClick={() => setAideOuverte((o) => !o)}
            className="flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
          >
            Où trouver ça ? <ChevronDown className={cn("size-3 transition-transform", aideOuverte && "rotate-180")} />
          </button>
          {aideOuverte && <p className="mt-1 rounded-md bg-muted/50 p-2 text-xs text-muted-foreground">{aide}</p>}
        </div>
      )}
    </div>
  );
}

/** Valeur à recopier ailleurs (l'URL de callback Discord, une variable, une commande). */
export function BoiteACopier({ valeur, libelle = "Copier l'URL de callback" }: { valeur: string; libelle?: string }) {
  const [copie, setCopie] = useState(false);
  return (
    <div className="flex items-center gap-2 rounded-md border bg-muted/40 px-3 py-2">
      <code className="min-w-0 flex-1 break-all text-xs">{valeur}</code>
      <button
        type="button"
        aria-label={libelle}
        className="text-muted-foreground hover:text-foreground"
        onClick={() => {
          void navigator.clipboard?.writeText(valeur);
          setCopie(true);
          setTimeout(() => setCopie(false), 1200);
        }}
      >
        {copie ? <Check className="size-4" /> : <Copy className="size-4" />}
      </button>
    </div>
  );
}
