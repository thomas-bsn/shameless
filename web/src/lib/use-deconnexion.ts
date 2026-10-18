import { useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { $api } from "@/api/client";
import { sessionQuery } from "@/lib/session";

/** La déconnexion : termine la session, vide le cache et renvoie à l'accueil. */
export function useDeconnexion() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  return $api.useMutation("post", "/auth/logout", {
    onSuccess: async () => {
      queryClient.removeQueries();
      queryClient.setQueryData(sessionQuery.queryKey, null);
      await navigate({ to: "/" });
    },
  });
}
