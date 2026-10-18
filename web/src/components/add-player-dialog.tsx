import { useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import { useState, type FormEvent } from "react";
import { $api } from "@/api/client";
import { ErrorAlert } from "@/components/query-state";
import { RankBadge, RoleIcon } from "@/components/rank";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { roleLabel } from "@/lib/ranks";

/** Ajout d'un Joueur : prénom et RiotId, compte Riot vérifié (rang, rôle) avant l'enregistrement. */
export function AddPlayerDialog({ open, onOpenChange }: { open: boolean; onOpenChange: (open: boolean) => void }) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <AddPlayerForm onCancel={() => onOpenChange(false)} onSaved={() => onOpenChange(false)} />
      </DialogContent>
    </Dialog>
  );
}

function AddPlayerForm({ onCancel, onSaved }: { onCancel: () => void; onSaved: () => void }) {
  const [prenom, setPrenom] = useState("");
  const [riotId, setRiotId] = useState("");
  // Le RiotId vérifié : modifier le champ ensuite masque le résultat.
  const [checked, setChecked] = useState<string | null>(null);
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const isCurrent = checked !== null && checked === riotId.trim();
  const preview = $api.useQuery(
    "get",
    "/players/riot-preview",
    { params: { query: { riotId: checked ?? "" } } },
    { enabled: isCurrent, staleTime: 0 },
  );
  const save = $api.useMutation("post", "/players", {
    onSuccess: async (created) => {
      await queryClient.invalidateQueries({ queryKey: ["get", "/players"] });
      onSaved();
      await navigate({ to: "/joueurs/$playerId", params: { playerId: String(created.id) } });
    },
  });

  function onCheck(event: FormEvent) {
    event.preventDefault();
    setChecked(riotId.trim());
  }

  return (
    <>
      <DialogHeader>
        <DialogTitle>Ajouter un joueur</DialogTitle>
        <DialogDescription>Le compte Riot est vérifié avant l'enregistrement.</DialogDescription>
      </DialogHeader>
      <form onSubmit={onCheck} className="flex flex-col gap-4">
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
          Vérifier le compte
        </Button>
      </form>

      {isCurrent && preview.isError && <ErrorAlert error={preview.error} />}
      {isCurrent && preview.isSuccess && (
        <section aria-labelledby="compte-riot">
          <Card>
            <CardContent className="flex flex-col gap-2">
              <p id="compte-riot" className="font-medium">
                Compte Riot trouvé
              </p>
              <p className="text-sm text-muted-foreground">{preview.data.riotId}</p>
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
            </CardContent>
          </Card>
        </section>
      )}
      {save.isError && <ErrorAlert error={save.error} />}
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onCancel}>
          Annuler
        </Button>
        {isCurrent && preview.isSuccess && (
          <Button
            disabled={preview.data.alreadyRegistered || !prenom.trim() || save.isPending}
            onClick={() => save.mutate({ body: { prenom: prenom.trim(), riotId: preview.data.riotId } })}
          >
            Ajouter {prenom.trim()}
          </Button>
        )}
      </DialogFooter>
    </>
  );
}
