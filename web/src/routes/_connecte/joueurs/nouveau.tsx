import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useState, type FormEvent } from "react";
import { $api } from "@/api/client";
import { PageHeader } from "@/components/page-header";
import { ErrorAlert } from "@/components/query-state";
import { RankBadge, RoleIcon } from "@/components/rank";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { roleLabel } from "@/lib/ranks";

export const Route = createFileRoute("/_connecte/joueurs/nouveau")({
  component: NouveauJoueur,
});

function NouveauJoueur() {
  const [prenom, setPrenom] = useState("");
  const [riotId, setRiotId] = useState("");
  // Le RiotId prévisualisé : modifier le champ ensuite masque l'aperçu.
  const [previewed, setPreviewed] = useState<string | null>(null);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const isCurrent = previewed !== null && previewed === riotId.trim();
  const preview = $api.useQuery(
    "get",
    "/players/riot-preview",
    { params: { query: { riotId: previewed ?? "" } } },
    { enabled: isCurrent, staleTime: 0 },
  );
  const save = $api.useMutation("post", "/players", {
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ["get", "/players"] });
      await navigate({ to: "/joueurs/$playerId", params: { playerId: String(created.id) } });
    },
  });

  function onPreview(event: FormEvent) {
    event.preventDefault();
    setPreviewed(riotId.trim());
  }

  return (
    <div className="flex max-w-xl flex-col gap-6">
      <Link to="/joueurs" className="text-sm text-muted-foreground hover:text-foreground">
        ← Joueurs
      </Link>
      <PageHeader title="Ajouter un joueur" description="Le compte Riot est vérifié avant l'enregistrement." />
      <form onSubmit={onPreview} className="flex flex-col gap-4">
        <div className="flex flex-col gap-2">
          <Label htmlFor="prenom">Prénom</Label>
          <Input id="prenom" value={prenom} onChange={(e) => setPrenom(e.target.value)} required />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="riotId">RiotId</Label>
          <Input
            id="riotId"
            placeholder="Pseudo#TAG"
            value={riotId}
            onChange={(e) => setRiotId(e.target.value)}
            required
          />
        </div>
        <Button type="submit" variant="outline" className="self-start" disabled={!riotId.trim()}>
          Prévisualiser
        </Button>
      </form>

      {isCurrent && preview.isError && <ErrorAlert error={preview.error} />}
      {isCurrent && preview.isSuccess && (
        <section aria-labelledby="compte-riot">
          <Card>
            <CardHeader>
              <CardTitle id="compte-riot">Compte Riot</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2">
              <p className="font-medium">{preview.data.riotId}</p>
              <RankBadge
                tier={preview.data.rankTier}
                division={preview.data.rankDivision}
                lp={preview.data.lp}
                className="self-start"
              />
              <p className="flex items-center gap-2 text-sm">
                <RoleIcon role={preview.data.mainRole} />
                {roleLabel(preview.data.mainRole) ?? "Rôle principal inconnu"}
              </p>
              {preview.data.alreadyRegistered && (
                <Alert>
                  <AlertDescription>Ce compte Riot est déjà un Joueur.</AlertDescription>
                </Alert>
              )}
              {save.isError && <ErrorAlert error={save.error} />}
            </CardContent>
            <CardFooter>
              <Button
                disabled={preview.data.alreadyRegistered || !prenom.trim() || save.isPending}
                onClick={() => save.mutate({ body: { prenom: prenom.trim(), riotId: preview.data.riotId } })}
              >
                Enregistrer
              </Button>
            </CardFooter>
          </Card>
        </section>
      )}
    </div>
  );
}
