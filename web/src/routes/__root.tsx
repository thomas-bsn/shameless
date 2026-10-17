import type { QueryClient } from "@tanstack/react-query";
import { createRootRouteWithContext, Link, Outlet, useRouterState } from "@tanstack/react-router";
import { SessionMenu } from "@/components/session-menu";
import { pageParLien } from "@/lib/liens";

export interface RouterContext {
  queryClient: QueryClient;
}

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootLayout,
  notFoundComponent: () => <p className="text-muted-foreground">Cette page n'existe pas.</p>,
});

const navigation = [
  { to: "/soiree", label: "Soirée" },
  { to: "/joueurs", label: "Joueurs" },
  { to: "/classements", label: "Classements" },
  { to: "/historique", label: "Historique" },
  { to: "/clips", label: "Clips" },
] as const;

function RootLayout() {
  // Les pages ouvertes par un lien d'accès (ADR 0010) n'ont pas de session : pas de « Se connecter ».
  const parLien = useRouterState({ select: (state) => pageParLien(state.location.pathname) });

  return (
    <div className="min-h-screen">
      <header className="border-b">
        <div className="mx-auto flex max-w-6xl items-center gap-6 px-4 py-3">
          <Link to="/" className="flex items-center gap-2 font-bold">
            <img src="/logo.png" alt="" className="size-8 rounded-full object-cover" />
            Shameless
          </Link>
          <nav className="flex gap-1 text-sm">
            {navigation.map((item) => (
              <Link
                key={item.to}
                to={item.to}
                className="rounded-md px-3 py-1.5 text-muted-foreground hover:bg-accent hover:text-accent-foreground"
                activeProps={{ className: "bg-accent text-accent-foreground" }}
              >
                {item.label}
              </Link>
            ))}
          </nav>
          {!parLien && (
            <div className="ml-auto">
              <SessionMenu />
            </div>
          )}
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-8">
        <Outlet />
      </main>
    </div>
  );
}
