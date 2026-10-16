import { Check, Copy } from "lucide-react";
import { useState, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

/** Une valeur secrète en lecture seule (lien, code), avec un bouton pour la copier et des actions en plus. */
export function CopyField({ id, label, value, children }: { id: string; label: string; value: string; children?: ReactNode }) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    await navigator.clipboard.writeText(value);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      <div className="flex flex-wrap gap-2">
        <Input id={id} readOnly value={value} className="min-w-64 flex-1 font-mono text-xs" onFocus={(e) => e.target.select()} />
        <Button variant="outline" onClick={() => void copy()}>
          {copied ? <Check /> : <Copy />}
          {copied ? "Copié" : "Copier"}
        </Button>
        {children}
      </div>
    </div>
  );
}
