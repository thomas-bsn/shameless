import { Badge } from "@/components/ui/badge";
import { $api } from "@/api/client";

export function ApiStatus() {
  const health = $api.useQuery("get", "/health", { parseAs: "text" });

  return (
    <p className="flex items-center gap-2 text-sm text-muted-foreground">
      État de l'API :
      {health.isPending ? (
        <Badge variant="outline">…</Badge>
      ) : health.isSuccess ? (
        <Badge variant="secondary">{health.data}</Badge>
      ) : (
        <Badge variant="destructive">Injoignable</Badge>
      )}
    </p>
  );
}
