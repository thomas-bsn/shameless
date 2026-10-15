import { queryOptions } from "@tanstack/react-query";
import { fetchClient } from "@/api/client";

/** Chemin local uniquement, comme l'exige l'API (ReturnUrlPolicy). */
const isLocalPath = (path: string) => path.startsWith("/") && !path.startsWith("//") && !path.startsWith("/\\");

/** Lance le login Discord de l'API ; elle redirige ensuite vers `returnTo`. */
export function loginUrl(returnTo: string): string {
  const returnUrl = isLocalPath(returnTo) ? returnTo : "/";
  return `/api/auth/discord/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

/** L'utilisateur connecté, ou null sans session. */
export const sessionQuery = queryOptions({
  queryKey: ["session"],
  queryFn: async () => {
    const { data, error, response } = await fetchClient.GET("/auth/me");
    if (response.status === 401) return null;
    if (error) throw error;
    return data;
  },
  staleTime: 5 * 60_000,
});
