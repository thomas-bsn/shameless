import { createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/classements")({
  component: () => <p className="text-muted-foreground">Bientôt.</p>,
});
