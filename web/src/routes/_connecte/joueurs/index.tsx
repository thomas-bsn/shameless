import { createFileRoute } from "@tanstack/react-router";
import { SearchIcon } from "lucide-react";
import { useMemo, useState } from "react";
import { $api } from "@/api/client";
import { AddPlayerDialog } from "@/components/add-player-dialog";
import { PageHeader } from "@/components/page-header";
import { PlayersTable } from "@/components/players-table";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { matchesPlayerOrRole } from "@/lib/players";

export const Route = createFileRoute("/_connecte/joueurs/")({
  component: Joueurs,
});

function Joueurs() {
  const players = $api.useQuery("get", "/players");
  // Le winrate en customs vient du classement 5v5 ; s'il manque, le tableau s'affiche sans.
  const classement = $api.useQuery("get", "/classements/5v5");
  const [search, setSearch] = useState("");
  const [adding, setAdding] = useState(false);

  const winrates = useMemo(() => {
    const map = new Map<number, number>();
    for (const ligne of classement.data?.joueurs ?? []) {
      if (ligne.winrate !== null) map.set(ligne.playerId, ligne.winrate);
    }
    return map;
  }, [classement.data]);
  const shown = useMemo(
    () => (players.data ?? []).filter((p) => matchesPlayerOrRole(p, search)),
    [players.data, search],
  );

  return (
    <>
      <PageHeader
        title="Joueurs"
        description={players.data ? `${players.data.length} joueurs` : undefined}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <div className="relative">
              <SearchIcon className="pointer-events-none absolute top-2.5 left-2.5 size-4 text-muted-foreground" aria-hidden />
              <Input
                type="search"
                aria-label="Chercher un joueur"
                placeholder="Chercher un joueur…"
                className="w-56 pl-8"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
              />
            </div>
            <Button onClick={() => setAdding(true)}>Ajouter un joueur</Button>
          </div>
        }
      />
      {players.isPending ? (
        <LoadingRows />
      ) : players.isError ? (
        <ErrorAlert error={players.error} />
      ) : shown.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          {players.data.length === 0 ? "Aucun joueur pour l'instant." : "Aucun joueur ne correspond."}
        </p>
      ) : (
        <Card className="p-2">
          <PlayersTable players={shown} winrates={winrates} />
        </Card>
      )}
      <AddPlayerDialog open={adding} onOpenChange={setAdding} />
    </>
  );
}
