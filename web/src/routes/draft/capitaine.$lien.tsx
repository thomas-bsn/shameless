import { createFileRoute } from "@tanstack/react-router";
import { DraftParLien } from "@/components/draft-champions-view";

/** Page publique, ouverte par un Lien capitaine : la Draft de champions de son Équipe. */
export const Route = createFileRoute("/draft/capitaine/$lien")({
  component: CapitainePage,
});

function CapitainePage() {
  const { lien } = Route.useParams();
  return <DraftParLien lien={lien} portee="Capitaine" />;
}
