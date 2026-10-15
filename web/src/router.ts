import { QueryCache, QueryClient } from "@tanstack/react-query";
import { createRouter, type RouterHistory } from "@tanstack/react-router";
import { problemStatus } from "@/lib/problem";
import { sessionQuery } from "@/lib/session";
import { routeTree } from "./routeTree.gen";

/** Le routeur et son cache de requêtes. Une session expirée renvoie vers la connexion. */
export function createApp(history?: RouterHistory) {
  const queryClient: QueryClient = new QueryClient({
    defaultOptions: { queries: { staleTime: 30_000, retry: false } },
    queryCache: new QueryCache({
      onError: (error, query) => {
        if (problemStatus(error) !== 401 || query.queryKey[0] === sessionQuery.queryKey[0]) return;
        queryClient.setQueryData(sessionQuery.queryKey, null);
        void router.invalidate();
      },
    }),
  });

  const router = createRouter({
    routeTree,
    history,
    context: { queryClient },
    defaultPreload: "intent",
    defaultPreloadStaleTime: 0,
    scrollRestoration: true,
  });

  return { queryClient, router };
}

declare module "@tanstack/react-router" {
  interface Register {
    router: ReturnType<typeof createApp>["router"];
  }
}
