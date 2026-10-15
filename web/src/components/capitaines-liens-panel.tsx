import { useQueryClient } from "@tanstack/react-query";
import { Check, Copy, Crown, RefreshCw } from "lucide-react";
import { useState, type FormEvent, type ReactNode } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { lienCapitaineUrl, lienSpectateurUrl } from "@/lib/liens";

type Series = Schemas["SeriesResponse"];
type Designation = Schemas["DesignationCapitaine"];
type Equipe = "A" | "B";

const designationLabels: Record<Designation, string> = {
  Manuel: "Au choix",
  AuHasard: "Au hasard",
  PlusGrosCost: "Plus gros cost",
};

/**
 * Les capitaines de la Série et ses liens d'accès (ADR 0010) : l'organisateur désigne un capitaine
 * par Équipe, copie ou renouvelle chaque Lien capitaine, indique les avoir envoyés, puis copie le
 * Lien spectateur.
 */
export function CapitainesLiensPanel({ series, onSeriesChanged }: { series: Series; onSeriesChanged: () => unknown }) {
  const queryClient = useQueryClient();
  const params = { params: { path: { id: series.seriesId } } };
  const liens = $api.useQuery("get", "/series/{id}/liens", params);
  const onLiensChanged = (data: Schemas["LiensSerieResponse"]) =>
    queryClient.setQueryData(["get", "/series/{id}/liens", params], data);
  const renouveler = $api.useMutation("post", "/series/{id}/liens/{equipe}/renouveler", { onSuccess: onLiensChanged });
  const envoyes = $api.useMutation("post", "/series/{id}/liens/envoyes", { onSuccess: onLiensChanged });
  const error = renouveler.error ?? envoyes.error;

  return (
    <section aria-labelledby="capitaines-liens">
      <Card>
        <CardHeader>
          <CardTitle id="capitaines-liens">Capitaines et liens</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-6">
          <div className="grid gap-4 sm:grid-cols-2">
            {(["A", "B"] as const).map((equipe) => (
              <CapitaineForm key={equipe} series={series} equipe={equipe} onDesigne={onSeriesChanged} />
            ))}
          </div>

          {liens.isPending ? (
            <LoadingRows />
          ) : liens.isError ? (
            <ErrorAlert error={liens.error} />
          ) : (
            <div className="flex flex-col gap-4">
              {(["A", "B"] as const).map((equipe) => (
                <LienField
                  key={equipe}
                  id={`lien-capitaine-${equipe}`}
                  label={`Lien capitaine de l'Équipe ${equipe}`}
                  url={lienCapitaineUrl(equipe === "A" ? liens.data.lienCapitaineA : liens.data.lienCapitaineB)}
                >
                  <Button
                    variant="outline"
                    disabled={renouveler.isPending}
                    onClick={() => {
                      if (window.confirm(`Renouveler le Lien capitaine de l'Équipe ${equipe} ? L'ancien ne marchera plus.`))
                        renouveler.mutate({ params: { path: { id: series.seriesId, equipe } } });
                    }}
                  >
                    <RefreshCw />
                    Renouveler
                  </Button>
                </LienField>
              ))}

              {liens.data.lienSpectateur ? (
                <>
                  <p className="flex items-center gap-2 text-sm text-muted-foreground">
                    <Badge variant="secondary">Liens capitaine envoyés</Badge>
                  </p>
                  <LienField
                    id="lien-spectateur"
                    label="Lien spectateur"
                    url={lienSpectateurUrl(liens.data.lienSpectateur)}
                  />
                </>
              ) : (
                <div className="flex flex-col gap-2">
                  <p className="text-sm text-muted-foreground">
                    Le Lien spectateur sera disponible une fois les Liens capitaine envoyés.
                  </p>
                  <Button
                    className="self-start"
                    disabled={envoyes.isPending}
                    onClick={() => envoyes.mutate({ params: { path: { id: series.seriesId } } })}
                  >
                    J'ai envoyé les Liens capitaine
                  </Button>
                </div>
              )}
              {error && <ErrorAlert error={error} />}
            </div>
          )}
        </CardContent>
      </Card>
    </section>
  );
}

/** Désigne le capitaine d'une Équipe : un joueur choisi, un joueur au hasard ou le plus gros cost. */
function CapitaineForm({ series, equipe, onDesigne }: { series: Series; equipe: Equipe; onDesigne: () => unknown }) {
  const players = equipe === "A" ? series.teamA : series.teamB;
  const capitaineId = equipe === "A" ? series.capitaineA : series.capitaineB;
  const capitaine = players.find((p) => p.playerId === capitaineId);
  const [designation, setDesignation] = useState<Designation>("Manuel");
  const [playerId, setPlayerId] = useState("");
  const designer = $api.useMutation("put", "/series/{id}/capitaines/{equipe}", { onSuccess: onDesigne });
  const manuel = designation === "Manuel";

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    designer.mutate({
      params: { path: { id: series.seriesId, equipe } },
      body: { designation, playerId: manuel ? Number(playerId) : null },
    });
  }

  return (
    <form onSubmit={onSubmit} aria-label={`Capitaine de l'Équipe ${equipe}`} className="flex flex-col gap-3">
      <p className="flex items-center gap-2 text-sm font-medium">
        Équipe {equipe} :
        {capitaine ? (
          <span className="flex items-center gap-1">
            <Crown className="size-4 text-amber-400" aria-hidden />
            {capitaine.prenom}
          </span>
        ) : (
          <span className="text-muted-foreground">pas de capitaine</span>
        )}
      </p>
      <div className="flex flex-col gap-2">
        <Label htmlFor={`designation-${equipe}`}>Désignation</Label>
        <Select value={designation} onValueChange={(value) => setDesignation(value as Designation)}>
          <SelectTrigger id={`designation-${equipe}`} className="w-full">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {(Object.keys(designationLabels) as Designation[]).map((d) => (
              <SelectItem key={d} value={d}>
                {designationLabels[d]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      {manuel && (
        <div className="flex flex-col gap-2">
          <Label htmlFor={`capitaine-${equipe}`}>Joueur</Label>
          <Select value={playerId} onValueChange={setPlayerId}>
            <SelectTrigger id={`capitaine-${equipe}`} className="w-full">
              <SelectValue placeholder="Choisir…" />
            </SelectTrigger>
            <SelectContent>
              {players.map((p) => (
                <SelectItem key={p.playerId} value={String(p.playerId)}>
                  {p.prenom}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}
      <Button type="submit" variant="outline" className="self-start" disabled={designer.isPending || (manuel && !playerId)}>
        Désigner le capitaine
      </Button>
      {designer.isError && <ErrorAlert error={designer.error} />}
    </form>
  );
}

/** Un lien en lecture seule, avec un bouton pour le copier et des actions en plus. */
function LienField({ id, label, url, children }: { id: string; label: string; url: string; children?: ReactNode }) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    await navigator.clipboard.writeText(url);
    setCopied(true);
    window.setTimeout(() => setCopied(false), 2000);
  }

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={id}>{label}</Label>
      <div className="flex flex-wrap gap-2">
        <Input id={id} readOnly value={url} className="min-w-64 flex-1 font-mono text-xs" onFocus={(e) => e.target.select()} />
        <Button variant="outline" onClick={() => void copy()}>
          {copied ? <Check /> : <Copy />}
          {copied ? "Copié" : "Copier"}
        </Button>
        {children}
      </div>
    </div>
  );
}
