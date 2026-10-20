import { cn } from "@/lib/utils";

/** L'emplacement d'une icône inconnue : id absent, id que le catalogue ne connaît pas, ou catalogue indisponible. */
export function EmplacementNeutre({ className }: { className?: string }) {
  return (
    <span
      role="img"
      aria-label="Inconnu"
      className={cn("inline-block size-7 shrink-0 rounded-md border border-dashed border-border bg-muted/20", className)}
    />
  );
}

/** Une icône Data Dragon à l'adresse donnée, ou l'emplacement neutre sans adresse. */
export function ImageDeJeu({ url, nom, className }: { url?: string; nom?: string; className?: string }) {
  if (!url) return <EmplacementNeutre className={className} />;
  return (
    <img
      src={url}
      alt={nom ?? ""}
      title={nom}
      className={cn("inline-block size-7 shrink-0 rounded-md object-cover", className)}
    />
  );
}
