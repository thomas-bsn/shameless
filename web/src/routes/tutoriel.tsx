import { createFileRoute } from "@tanstack/react-router";
import { PageHeader } from "@/components/page-header";
import { SujetDuTutoriel, sujets } from "@/components/tutoriel-sujets";

/** Page publique : pas de session requise. */
export const Route = createFileRoute("/tutoriel")({
  component: Tutoriel,
});

function Tutoriel() {
  return (
    <div className="mx-auto max-w-3xl">
      <PageHeader title="Tutoriel" description="Qui est quoi, et à quoi servent les outils de Shameless." />
      <div className="flex flex-col gap-4">
        {sujets.map((sujet) => (
          <SujetDuTutoriel key={sujet.id} sujet={sujet} />
        ))}
      </div>
    </div>
  );
}
