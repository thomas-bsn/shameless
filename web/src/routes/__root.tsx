import type { QueryClient } from "@tanstack/react-query";
import { createRootRouteWithContext, Outlet, useRouterState } from "@tanstack/react-router";
import { AppShell, MinimalShell } from "@/components/app-shell";
import { pageParLien } from "@/lib/liens";
import { useTheme } from "@/lib/theme";

export interface RouterContext {
  queryClient: QueryClient;
}

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootLayout,
  notFoundComponent: () => <p className="text-muted-foreground">Cette page n'existe pas.</p>,
});

function RootLayout() {
  // Le thème s'applique à toutes les pages, y compris celles ouvertes par un lien d'accès.
  useTheme();
  // Les pages ouvertes par un lien d'accès (ADR 0010) n'ont ni session ni sidebar.
  const parLien = useRouterState({ select: (state) => pageParLien(state.location.pathname) });
  const Shell = parLien ? MinimalShell : AppShell;

  return (
    <Shell>
      <Outlet />
    </Shell>
  );
}
