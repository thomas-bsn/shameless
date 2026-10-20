import { useCatalogue } from "@/lib/catalogue";
import { useChampions } from "@/lib/draft-champions-queries";
import { ImageDeJeu } from "./emplacement-neutre";

type Props = { className?: string };

/** Le portrait d'un champion (id Data Dragon, « LeeSin »). */
export function IconeChampion({ championId, className }: Props & { championId: string | null | undefined }) {
  const champions = useChampions();
  const champion = championId ? champions.data?.find((c) => c.id === championId) : undefined;
  return <ImageDeJeu url={champion?.imageUrl} nom={champion?.name} className={className} />;
}

/** Un sort d'invocateur (id du client, `spell1Id`). */
export function IconeSort({ sortId, className }: Props & { sortId: number | null | undefined }) {
  const { sort } = useCatalogue();
  const element = sortId == null ? undefined : sort(sortId);
  return <ImageDeJeu url={element?.iconUrl} nom={element?.name} className={className} />;
}

/** Un objet (id du client). */
export function IconeObjet({ objetId, className }: Props & { objetId: number | null | undefined }) {
  const { objet } = useCatalogue();
  const element = objetId == null ? undefined : objet(objetId);
  return <ImageDeJeu url={element?.iconUrl} nom={element?.name} className={className} />;
}

/** Une rune ou un arbre de runes (id du client). */
export function IconeRune({ runeId, className }: Props & { runeId: number | null | undefined }) {
  const { rune, arbre } = useCatalogue();
  const element = runeId == null ? undefined : (rune(runeId) ?? arbre(runeId));
  return <ImageDeJeu url={element?.iconUrl} nom={element?.name} className={className} />;
}

export type ToucheDeCapacite = "Q" | "W" | "E" | "R";

/** Une capacité d'un champion (Q, W, E ou R), d'après le catalogue. */
export function IconeCapacite({
  championId,
  touche,
  className,
}: Props & { championId: string | null | undefined; touche: ToucheDeCapacite }) {
  const { capacite } = useCatalogue();
  const element = championId ? capacite(championId, touche) : undefined;
  return (
    <ImageDeJeu url={element?.iconUrl} nom={element ? `${touche} : ${element.name}` : undefined} className={className} />
  );
}
