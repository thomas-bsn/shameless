import { createFileRoute } from "@tanstack/react-router";
import { DraftParLien } from "@/components/draft-champions-view";

/** Page publique, ouverte par le Lien spectateur : la Draft de champions en lecture seule. */
export const Route = createFileRoute("/draft/spectateur/$lien")({
  component: SpectateurPage,
});

function SpectateurPage() {
  const { lien } = Route.useParams();
  return <DraftParLien lien={lien} portee="Spectateur" />;
}
