import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { PanneauVerre } from "@/components/panneau-verre";
import { SelecteurDeTheme } from "@/components/selecteur-de-theme";
import { SessionMenu } from "@/components/session-menu";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { sessionQuery } from "@/lib/session";
import { cn } from "@/lib/utils";

const navigation = [
  { to: "/soiree", label: "Soirée" },
  { to: "/joueurs", label: "Joueurs" },
  { to: "/classements", label: "Classements" },
  { to: "/historique", label: "Historique" },
  { to: "/clips", label: "Clips" },
] as const;

const classeLien =
  "flex items-center gap-3 rounded-md px-3 py-2 text-sm text-muted-foreground hover:bg-muted hover:text-foreground data-[actif=oui]:bg-primary/10 data-[actif=oui]:font-medium data-[actif=oui]:text-primary";

/**
 * La sidebar : la photo de Sam, les cinq entrées, Administration pour les seuls Administrateurs,
 * l'état de connexion Discord et le sélecteur de thème. Sur écran étroit, c'est un tiroir.
 */
export function Sidebar({
  ouvert,
  onFermer,
}: {
  /** Le tiroir est ouvert (écran étroit) ; sans effet sur grand écran. */
  ouvert: boolean;
  /** Referme le tiroir : au voile, ou au choix d'une page. */
  onFermer: () => void;
}) {
  const session = useQuery(sessionQuery);
  const user = session.data;

  return (
    <>
      {ouvert && <div aria-hidden="true" onClick={onFermer} className="fixed inset-0 z-40 bg-black/50 md:hidden" />}
      <PanneauVerre asChild>
        <aside
          id="sidebar"
          className={cn(
            "fixed inset-y-0 left-0 z-50 flex w-72 flex-col gap-4 overflow-y-auto p-4 transition-transform md:sticky md:top-0 md:h-screen md:shrink-0 md:translate-x-0",
            ouvert ? "translate-x-0" : "-translate-x-full",
          )}
        >
          <Link to="/" onClick={onFermer} className="flex items-center gap-3 rounded-md p-1">
            <Avatar className="size-11">
              {/* Aucun portrait de Sam n'est versionné dans le dépôt : le repli visible est le logo. */}
              <AvatarFallback>
                <img src="/logo.png" alt="" className="size-full object-cover" />
              </AvatarFallback>
            </Avatar>
            <span className="text-base font-bold">Shameless</span>
          </Link>
          <nav aria-label="Navigation principale" className="flex flex-col gap-1">
            {navigation.map((entree) => (
              <Link
                key={entree.to}
                to={entree.to}
                onClick={onFermer}
                className={classeLien}
                activeProps={{ "data-actif": "oui" }}
              >
                {entree.label}
              </Link>
            ))}
          </nav>
          <div className="mt-auto flex flex-col gap-4">
            {user?.isAdmin && (
              <Link to="/administration" onClick={onFermer} className={classeLien} activeProps={{ "data-actif": "oui" }}>
                Administration
              </Link>
            )}
            <SessionMenu />
            <SelecteurDeTheme />
          </div>
        </aside>
      </PanneauVerre>
    </>
  );
}
