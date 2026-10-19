import { useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { $api } from "@/api/client";
import { AvatarJoueur } from "@/components/avatar-joueur";
import { ErrorAlert } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { POLLING_DRAFT_MS } from "@/lib/draft-champions-queries";
import { cn } from "@/lib/utils";

/** Longueur maximale d'un message, la même que côté API. */
const LONGUEUR_MAX = 300;

const CLE_SPECTATEUR = "shameless:spectateur-id";

let idEnMemoire: string | null = null;

/**
 * Le jeton qui garde le pseudo « Spectateur N » d'un spectateur sans connexion d'un message à l'autre.
 * Conservé dans le navigateur ; en mémoire seulement si le stockage est indisponible.
 */
export function spectateurId(): string {
  try {
    const existant = localStorage.getItem(CLE_SPECTATEUR);
    if (existant) return existant;
    const nouveau = crypto.randomUUID();
    localStorage.setItem(CLE_SPECTATEUR, nouveau);
    return nouveau;
  } catch {
    return (idEnMemoire ??= crypto.randomUUID());
  }
}

/**
 * Le chat de la draft, sur la page capitaine comme sur la page spectateur : les messages de la draft en
 * cours, relus régulièrement, et un champ pour écrire.
 */
export function ChatDeLaDraft({ lien }: { lien: string }) {
  const path = { params: { path: { lien } } };
  const queryClient = useQueryClient();
  const [texte, setTexte] = useState("");
  const fin = useRef<HTMLDivElement>(null);

  const messages = $api.useQuery(
    "get",
    "/liens/{lien}/draft-champions/messages",
    path,
    { refetchInterval: POLLING_DRAFT_MS, retry: false },
  );
  const ecrire = $api.useMutation("post", "/liens/{lien}/draft-champions/messages", {
    onSuccess: () => setTexte(""),
    onSettled: () =>
      queryClient.invalidateQueries({
        queryKey: ["get", "/liens/{lien}/draft-champions/messages"],
      }),
  });

  const liste = messages.data?.messages ?? [];
  useEffect(() => {
    fin.current?.scrollIntoView?.({ block: "end" });
  }, [liste.length]);

  const envoyer = (e: FormEvent) => {
    e.preventDefault();
    const contenu = texte.trim();
    if (!contenu) return;
    ecrire.mutate({ ...path, body: { contenu, spectateurId: spectateurId() } });
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Chat</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <div
          className="flex max-h-64 flex-col gap-2 overflow-y-auto"
          aria-live="polite"
        >
          {liste.length === 0 && (
            <p className="text-sm text-muted-foreground">Aucun message.</p>
          )}
          {liste.map((m) => (
            <div key={m.id} className="flex items-start gap-2 text-sm">
              <AvatarJoueur prenom={m.auteur} avatarUrl={m.avatarUrl} className="size-6 text-xs" />
              <div className="min-w-0">
                <span
                  className={cn(
                    "mr-2 font-semibold",
                    m.equipe && "text-primary",
                  )}
                >
                  {m.auteur}
                </span>
                <span className="break-words">{m.contenu}</span>
              </div>
            </div>
          ))}
          <div ref={fin} />
        </div>
        <form onSubmit={envoyer} className="flex gap-2">
          <Input
            aria-label="Message"
            placeholder="Écrire un message"
            value={texte}
            maxLength={LONGUEUR_MAX}
            onChange={(e) => setTexte(e.target.value)}
          />
          <Button type="submit" disabled={ecrire.isPending || !texte.trim()}>
            Envoyer
          </Button>
        </form>
        {ecrire.isError && <ErrorAlert error={ecrire.error} />}
      </CardContent>
    </Card>
  );
}
