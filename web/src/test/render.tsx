import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import {
  createMemoryHistory,
  createRootRouteWithContext,
  createRoute,
  createRouter,
  Outlet,
  RouterProvider,
} from "@tanstack/react-router";
import { render } from "@testing-library/react";
import type { ReactNode } from "react";
import { createApp } from "@/router";

const newQueryClient = () => new QueryClient({ defaultOptions: { queries: { retry: false } } });

/** Rend un composant isolé, avec React Query et un routeur minimal (pour les Link). */
export function renderComponent(ui: ReactNode) {
  const queryClient = newQueryClient();
  const rootRoute = createRootRouteWithContext<{ queryClient: QueryClient }>()({ component: Outlet });
  const indexRoute = createRoute({ getParentRoute: () => rootRoute, path: "/", component: () => ui });
  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute]),
    context: { queryClient },
    history: createMemoryHistory({ initialEntries: ["/"] }),
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
}

/** Rend l'application complète sur une URL donnée. */
export function renderApp(url: string) {
  const { queryClient, router } = createApp(createMemoryHistory({ initialEntries: [url] }));
  const view = render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );
  return { ...view, router, queryClient };
}
