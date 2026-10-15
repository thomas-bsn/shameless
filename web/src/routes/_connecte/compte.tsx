import { useQuery, useQueryClient } from "@tanstack/react-query";
import { createFileRoute } from "@tanstack/react-router";
import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { matchesPlayer, myPlayerQuery } from "@/lib/players";
import { problemMessage } from "@/lib/problem";
import { rankLabel } from "@/lib/ranks";

export const Route = createFileRoute("/_connecte/compte")({
  component: Compte,
});

function Compte() {
  const { user } = Route.useRouteContext();

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-center gap-4">
        <Avatar className="size-16">
          {user.avatarUrl && <AvatarImage src={user.avatarUrl} alt="" />}
          <AvatarFallback>{user.username?.charAt(0).toUpperCase()}</AvatarFallback>
        </Avatar>
        <div>
          <h1 className="text-2xl font-bold">{user.username}</h1>
          <p className="text-sm text-muted-foreground">
            Compte Discord {user.isAdmin && <Badge variant="secondary">Admin</Badge>}
          </p>
        </div>
      </div>
      <LinkedPlayer />
    </div>
  );
}

function LinkedPlayer() {
  const myPlayer = useQuery(myPlayerQuery);

  return (
    <section aria-labelledby="joueur-lie" className="flex flex-col gap-4">
      <Card>
        <CardHeader>
          <CardTitle id="joueur-lie">Joueur lié</CardTitle>
        </CardHeader>
        <CardContent>
          {myPlayer.isPending ? (
            <Skeleton className="h-10 w-64" />
          ) : myPlayer.isError ? (
            <Alert variant="destructive">
              <AlertDescription>{problemMessage(myPlayer.error)}</AlertDescription>
            </Alert>
          ) : myPlayer.data ? (
            <PlayerSummary player={myPlayer.data} />
          ) : (
            <p className="text-muted-foreground">Aucun joueur n'est lié à ton compte Discord.</p>
          )}
        </CardContent>
      </Card>
      {myPlayer.isSuccess && <LinkForm linkedPlayerId={myPlayer.data?.id} />}
    </section>
  );
}

function PlayerSummary({ player }: { player: Schemas["PlayerDto"] }) {
  return (
    <div>
      <p className="font-semibold">{player.prenom}</p>
      <p className="text-sm text-muted-foreground">{player.riotId}</p>
      <p className="text-sm">{rankLabel(player.rankTier, player.rankDivision, player.lp)}</p>
    </div>
  );
}

function LinkForm({ linkedPlayerId }: { linkedPlayerId?: number }) {
  const [search, setSearch] = useState("");
  const queryClient = useQueryClient();
  const players = $api.useQuery("get", "/players");
  const link = $api.useMutation("post", "/players/link", {
    onSuccess: () => queryClient.invalidateQueries({ queryKey: myPlayerQuery.queryKey }),
  });

  const candidates = (players.data ?? []).filter((p) => p.id !== linkedPlayerId && matchesPlayer(p, search));

  return (
    <Card>
      <CardHeader>
        <CardTitle>{linkedPlayerId ? "Changer de joueur" : "Lier mon compte à un joueur"}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <Input
          type="search"
          aria-label="Chercher un joueur"
          placeholder="Prénom ou RiotId"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        {link.isError && (
          <Alert variant="destructive">
            <AlertDescription>{problemMessage(link.error)}</AlertDescription>
          </Alert>
        )}
        {players.isError && (
          <Alert variant="destructive">
            <AlertDescription>{problemMessage(players.error)}</AlertDescription>
          </Alert>
        )}
        <ul className="flex max-h-80 flex-col divide-y overflow-y-auto">
          {candidates.map((p) => (
            <li key={p.id} className="flex items-center justify-between gap-4 py-2">
              <div>
                <p className="font-medium">{p.prenom}</p>
                <p className="text-xs text-muted-foreground">{p.riotId}</p>
              </div>
              <Button
                size="sm"
                variant="outline"
                aria-label={`Lier ${p.prenom}`}
                disabled={link.isPending}
                onClick={() => link.mutate({ body: { playerId: p.id } })}
              >
                Lier
              </Button>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
