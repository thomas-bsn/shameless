import { createFileRoute } from "@tanstack/react-router";
import { FilDeClips } from "@/components/fil-de-clips";
import { PageHeader } from "@/components/page-header";

/** Les Clips en fil par soirée, avec ceux sans Game à rattacher à la main (ADR 0014). */
export const Route = createFileRoute("/_connecte/clips")({
  component: Clips,
});

function Clips() {
  return (
    <>
      <PageHeader title="Clips" description="Les clips de la communauté, rangés par soirée puis par game." />
      <FilDeClips />
    </>
  );
}
