import { createFileRoute } from "@tanstack/react-router";
import { ApiStatus } from "@/components/api-status";

export const Route = createFileRoute("/")({
  component: Home,
});

function Home() {
  return (
    <section className="flex flex-col items-center gap-4 py-12 text-center">
      <img src="/logo.png" alt="" className="size-40 rounded-full object-cover" />
      <h1 className="text-4xl font-bold">Shameless</h1>
      <p className="text-muted-foreground">Les customs 5v5 entre potes : joueurs, séries, classements et historique.</p>
      <ApiStatus />
    </section>
  );
}
