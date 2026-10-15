import { createFileRoute } from "@tanstack/react-router";

export const Route = createFileRoute("/joueurs")({
  component: () => <p className="text-muted-foreground">Bientôt.</p>,
});
