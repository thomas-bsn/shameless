import type { QueryClient } from "@tanstack/react-query";
import { createRootRouteWithContext, Outlet, useRouterState } from "@tanstack/react-router";
import { useState } from "react";
import { Sidebar } from "@/components/sidebar";
import { Button } from "@/components/ui/button";
import { Menu } from "lucide-react";
import { pageParLien } from "@/lib/liens";

export interface RouterContext {
  queryClient: QueryClient;
}

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootLayout,
  notFoundComponent: () => <p className="text-muted-foreground">Cette page n'existe pas.</p>,
});

function RootLayout() {
  const parLien = useRouterState({ select: (state) => pageParLien(state.location.pathname) });
  const [tiroirOuvert, setTiroirOuvert] = useState(false);
  const fermerTiroir = () => setTiroirOuvert(false);

  // Les pages ouvertes par un lien d'accès (ADR 0010) n'ont ni sidebar ni session : un en-tête minimal.
  if (parLien) {
    return (
      <div className="min-h-screen">
        <header className="flex items-center gap-3 p-4">
          <img src="/logo.png" alt="" className="size-8 rounded-full object-cover" />
          <span className="font-bold">Shameless</span>
        </header>
        <main className="mx-auto max-w-6xl px-4 py-8">
          <Outlet />
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen md:flex">
      <Sidebar ouvert={tiroirOuvert} onFermer={fermerTiroir} />
      <div className="flex-1">
        <header className="flex items-center gap-2 px-3 py-2 md:hidden">
          <Button
            variant="outline"
            className="cible-tactile"
            aria-label={tiroirOuvert ? "Fermer le menu" : "Ouvrir le menu"}
            aria-expanded={tiroirOuvert}
            aria-controls="sidebar"
            onClick={() => setTiroirOuvert(!tiroirOuvert)}
          >
            <Menu aria-hidden />
          </Button>
          <span className="font-bold">Shameless</span>
        </header>
        <main className="mx-auto max-w-6xl px-4 py-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
