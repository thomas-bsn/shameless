import { createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/_connecte/joueurs")({
  component: () => <p className="text-muted-foreground">Bientôt.</p>,
});
