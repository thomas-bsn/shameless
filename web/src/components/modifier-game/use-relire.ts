import { useQueryClient } from "@tanstack/react-query";

/** Après une action : relire la liste du mois, la game et tout ce qui en dépend. */
export function useRelire() {
  const queryClient = useQueryClient();
  return () =>
    queryClient.invalidateQueries({
      predicate: (q) => typeof q.queryKey[1] === "string" && q.queryKey[1].startsWith("/games"),
    });
}
