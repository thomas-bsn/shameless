import { createFileRoute, useNavigate } from "@tanstack/react-router";
import { ClipsTous, FilDeClips } from "@/components/fil-de-clips";
import { PageHeader } from "@/components/page-header";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

type Vue = "soiree" | "tous";

/** Les Clips en fil par soirée, avec ceux sans Game à rattacher à la main (ADR 0014), ou à plat et filtrables. */
export const Route = createFileRoute("/_connecte/clips")({
  validateSearch: (search: Record<string, unknown>): { vue?: "tous" } => (search.vue === "tous" ? { vue: "tous" } : {}),
  component: Clips,
});

function Clips() {
  const { vue = "soiree" } = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });

  return (
    <>
      <PageHeader title="Clips" description="Les clips de la communauté, rangés par soirée puis par game." />
      <Tabs value={vue} onValueChange={(valeur) => navigate({ search: (valeur as Vue) === "tous" ? { vue: "tous" } : {} })} className="mb-5">
        <TabsList>
          <TabsTrigger value="soiree">Par soirée</TabsTrigger>
          <TabsTrigger value="tous">Tous</TabsTrigger>
        </TabsList>
      </Tabs>
      {vue === "tous" ? <ClipsTous /> : <FilDeClips />}
    </>
  );
}
