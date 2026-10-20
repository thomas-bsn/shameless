const COULEUR_HEX = /^#[0-9a-f]{6}$/i;

/** La bannière Discord d'un compte ; sans image, sa couleur d'accent, sinon un fond neutre. */
export function BanniereDiscord({ url, couleur }: { url: string | null | undefined; couleur: string | null | undefined }) {
  const accent = couleur && COULEUR_HEX.test(couleur) ? couleur : null;
  return (
    <div className="h-24 bg-muted sm:h-28">
      {url ? (
        <img src={url} alt="Bannière Discord" className="size-full object-cover" />
      ) : (
        accent && (
          <svg aria-hidden className="size-full">
            <rect width="100%" height="100%" fill={accent} />
          </svg>
        )
      )}
    </div>
  );
}
