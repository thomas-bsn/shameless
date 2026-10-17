// PROTOTYPE (issue #68) — route jetable : trois directions du wizard d'installation, ?variant=A|B|C.
import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ChevronLeft, ChevronRight, RotateCcw } from "lucide-react";
import { useEffect } from "react";
import { useWizard } from "@/prototype-wizard/state";
import type { UchuuVariant } from "@/prototype-wizard/uchuu";
import { VariantA } from "@/prototype-wizard/variant-a";
import { VariantB } from "@/prototype-wizard/variant-b";
import { VariantC } from "@/prototype-wizard/variant-c";

const VARIANTS = [
  { key: "A", name: "Pas à pas" },
  { key: "B", name: "Une seule page" },
  { key: "C", name: "Console" },
] as const;
type Key = (typeof VARIANTS)[number]["key"];

export const Route = createFileRoute("/prototype/wizard")({
  validateSearch: (s: Record<string, unknown>): { variant: Key; uchuu: UchuuVariant } => ({
    variant: s.variant === "B" || s.variant === "C" ? s.variant : "A",
    uchuu: s.uchuu === "2" || s.uchuu === "3" ? s.uchuu : "1",
  }),
  component: Page,
});

function Page() {
  const { variant, uchuu } = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  const w = useWizard();
  const index = VARIANTS.findIndex((v) => v.key === variant);
  const cycle = (d: number) => void navigate({ search: (p) => ({ ...p, variant: VARIANTS[(index + d + VARIANTS.length) % VARIANTS.length].key }), replace: true });
  const UCHUU: UchuuVariant[] = ["1", "2", "3"];
  const cycleU = (d: number) => void navigate({ search: (p) => ({ ...p, uchuu: UCHUU[(UCHUU.indexOf(uchuu) + d + 3) % 3] }), replace: true });

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      const t = e.target as HTMLElement;
      if (t.closest("input, textarea, select, [contenteditable]")) return;
      if (e.key === "ArrowLeft") cycle(-1);
      if (e.key === "ArrowRight") cycle(1);
      if (e.key === "ArrowUp") cycleU(1);
      if (e.key === "ArrowDown") cycleU(-1);
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  });

  return (
    // Le wizard est distinct du site : on recouvre l'en-tête de Shameless.
    <div className="fixed inset-0 z-40 overflow-y-auto bg-background">
      {variant === "A" && <VariantA w={w} uchuu={uchuu} />}
      {variant === "B" && <VariantB w={w} />}
      {variant === "C" && <VariantC w={w} />}
      <div className="fixed bottom-16 left-1/2 z-50 flex -translate-x-1/2 items-center gap-1 rounded-full bg-zinc-900 p-1 text-sm text-white shadow-xl ring-1 ring-white/20">
        <button aria-label="Uchuu précédent" onClick={() => cycleU(-1)} className="rounded-full p-2 hover:bg-white/10"><ChevronLeft className="size-4" /></button>
        <span className="min-w-44 text-center">Uchuu {uchuu} ({["Bloc .env", "Tableau", "3 actions"][UCHUU.indexOf(uchuu)]})</span>
        <button aria-label="Uchuu suivant" onClick={() => cycleU(1)} className="rounded-full p-2 hover:bg-white/10"><ChevronRight className="size-4" /></button>
      </div>
      <div className="fixed bottom-4 left-1/2 z-50 flex -translate-x-1/2 items-center gap-1 rounded-full bg-zinc-900 p-1 text-sm text-white shadow-xl ring-1 ring-white/20">
        <button aria-label="Variante précédente" onClick={() => cycle(-1)} className="rounded-full p-2 hover:bg-white/10"><ChevronLeft className="size-4" /></button>
        <span className="min-w-36 text-center">{variant} ({VARIANTS[index].name})</span>
        <button aria-label="Variante suivante" onClick={() => cycle(1)} className="rounded-full p-2 hover:bg-white/10"><ChevronRight className="size-4" /></button>
        <button aria-label="Recommencer" title="Recommencer le parcours" onClick={w.reset} className="rounded-full p-2 hover:bg-white/10"><RotateCcw className="size-4" /></button>
      </div>
    </div>
  );
}
