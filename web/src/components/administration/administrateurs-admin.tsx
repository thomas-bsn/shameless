import { useQueryClient } from "@tanstack/react-query";
import { ShieldCheckIcon, ShieldOffIcon } from "lucide-react";
import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

type Utilisateur = Schemas["UtilisateurResponse"];

const pseudoDe = (u: Utilisateur) => u.username ?? `Compte ${u.discordUserId}`;

const dateLabel = (iso: string) => new Date(iso).toLocaleDateString("fr-FR", { day: "2-digit", month: "2-digit", year: "numeric" });

function BadgeDuRole({ role }: { role: Utilisateur["role"] }) {
  if (role === "Superadministrateur") return <Badge>Superadmin</Badge>;
  if (role === "Administrateur") return <Badge variant="secondary">Admin</Badge>;
  return null;
}

function Promotion({ utilisateur }: { utilisateur: Utilisateur }) {
  if (utilisateur.role !== "Administrateur" || !utilisateur.promotedBy || !utilisateur.promotedAt) return null;
  return (
    <span className="text-xs text-muted-foreground">
      promu par {utilisateur.promotedBy} le {dateLabel(utilisateur.promotedAt)}
    </span>
  );
}

function Actions({ utilisateur, estMoi }: { utilisateur: Utilisateur; estMoi: boolean }) {
  const queryClient = useQueryClient();
  const options = { onSuccess: () => queryClient.invalidateQueries({ queryKey: ["get", "/utilisateurs"] }) };
  const promouvoir = $api.useMutation("put", "/utilisateurs/{discordId}/administrateur", options);
  const retirer = $api.useMutation("delete", "/utilisateurs/{discordId}/administrateur", options);
  const params = { path: { discordId: Number(utilisateur.discordUserId) } };
  const pseudo = pseudoDe(utilisateur);

  if (utilisateur.role === "Superadministrateur") {
    return <span className="text-xs text-muted-foreground">Superadministrateur : défini en ligne de commande, pas depuis le site.</span>;
  }
  if (estMoi) {
    return <span className="text-xs text-muted-foreground">Un Administrateur ne peut pas se retirer son propre rôle.</span>;
  }
  const erreur = promouvoir.error ?? retirer.error;
  return (
    <div className="flex flex-col items-start gap-2">
      {utilisateur.role === "Administrateur" ? (
        <Button variant="outline" size="sm" aria-label={`Retirer le rôle de ${pseudo}`} disabled={retirer.isPending} onClick={() => retirer.mutate({ params })}>
          <ShieldOffIcon />
          Retirer le rôle
        </Button>
      ) : (
        <Button size="sm" aria-label={`Promouvoir ${pseudo}`} disabled={promouvoir.isPending} onClick={() => promouvoir.mutate({ params })}>
          <ShieldCheckIcon />
          Promouvoir
        </Button>
      )}
      {erreur && <ErrorAlert error={erreur} />}
    </div>
  );
}

/** L'onglet Administrateurs : les rôles des Utilisateurs (ADR 0017). */
export function AdministrateursAdmin({ moiDiscordId }: { moiDiscordId: string | null | undefined }) {
  const utilisateurs = $api.useQuery("get", "/utilisateurs");
  const [recherche, setRecherche] = useState("");

  if (utilisateurs.isPending) return <LoadingRows />;
  if (utilisateurs.isError) return <ErrorAlert error={utilisateurs.error} />;

  const terme = recherche.trim().toLowerCase();
  const visibles = utilisateurs.data.filter((u) => pseudoDe(u).toLowerCase().includes(terme));

  return (
    <Card>
      <CardHeader>
        <CardTitle>Administrateurs</CardTitle>
        <CardDescription>
          Un Administrateur promeut un Utilisateur ou retire ce rôle à un autre Administrateur. Seuls les Utilisateurs déjà connectés
          apparaissent.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <Input
          type="search"
          aria-label="Rechercher un utilisateur"
          placeholder="Rechercher par pseudo"
          value={recherche}
          onChange={(e) => setRecherche(e.target.value)}
          className="max-w-sm"
        />
        {visibles.length === 0 ? (
          <p className="text-sm text-muted-foreground">Aucun utilisateur.</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Utilisateur</TableHead>
                <TableHead>Rôle</TableHead>
                <TableHead>Action</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {visibles.map((u) => (
                <TableRow key={u.discordUserId}>
                  <TableCell>
                    <div className="flex items-center gap-3">
                      <Avatar className="size-8">
                        {u.avatarUrl && <AvatarImage src={u.avatarUrl} alt="" />}
                        <AvatarFallback>{pseudoDe(u).slice(0, 2).toUpperCase()}</AvatarFallback>
                      </Avatar>
                      <span className="font-medium">{pseudoDe(u)}</span>
                    </div>
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col items-start gap-1">
                      <BadgeDuRole role={u.role} />
                      <Promotion utilisateur={u} />
                    </div>
                  </TableCell>
                  <TableCell className="whitespace-normal">
                    <Actions utilisateur={u} estMoi={u.discordUserId === moiDiscordId} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
