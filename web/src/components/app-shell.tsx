import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { MenuIcon } from "lucide-react";
import { type ReactNode, useState } from "react";
import { Fond } from "@/components/fond";
import { SessionMenu } from "@/components/session-menu";
import { Sheet, SheetContent, SheetDescription, SheetTitle, SheetTrigger } from "@/components/ui/sheet";
import { sessionQuery } from "@/lib/session";
import { cn } from "@/lib/utils";

const navigation = [
  { to: "/soiree", label: "Soirée" },
  { to: "/joueurs", label: "Joueurs" },
  { to: "/classements", label: "Classements" },
  { to: "/historique", label: "Historique" },
  { to: "/clips", label: "Clips" },
  { to: "/tutoriel", label: "Tutoriel" },
] as const;

const lienClasse = "flex items-center truncate rounded-md px-3 py-2 text-sm text-muted-foreground hover:bg-accent hover:text-accent-foreground";
const lienActif = "bg-accent text-accent-foreground";

function Marque({ className }: { className?: string }) {
  return (
    <Link to="/" className={cn("flex items-center gap-2 font-bold", className)}>
      <img src="/logo.png" alt="" className="size-9 rounded-full object-cover" />
      <span className="flex flex-col leading-tight">
        Shameless
        <span className="text-xs font-normal text-muted-foreground">Customs 5v5</span>
      </span>
    </Link>
  );
}

/** Le contenu de la sidebar, identique en colonne fixe et dans le tiroir mobile. */
function NavContent({ onNavigate }: { onNavigate?: () => void }) {
  const session = useQuery(sessionQuery);
  return (
    <div className="flex h-full min-w-0 flex-col gap-6">
      <Marque />
      <nav aria-label="Navigation principale" className="flex flex-col gap-1">
        {navigation.map((item) => (
          <Link key={item.to} to={item.to} onClick={onNavigate} className={lienClasse} activeProps={{ className: lienActif }}>
            {item.label}
          </Link>
        ))}
        {session.data?.isAdmin && (
          <Link to="/administration" onClick={onNavigate} className={lienClasse} activeProps={{ className: lienActif }}>
            Administration
          </Link>
        )}
      </nav>
      <div className="mt-auto">
        <SessionMenu />
      </div>
    </div>
  );
}

/** Le shell des pages à session : sidebar à gauche, tiroir sur écran étroit. */
export function AppShell({ children }: { children: ReactNode }) {
  const [ouvert, setOuvert] = useState(false);
  return (
    <div className="relative isolate min-h-screen md:flex">
      <Fond />
      <aside className="sticky top-0 hidden h-screen w-56 shrink-0 overflow-hidden bg-card/74 p-4 md:block dark:bg-card/42">
        <NavContent />
      </aside>
      <div className="min-w-0 flex-1">
        <header className="flex items-center gap-2 bg-card/74 p-2 md:hidden dark:bg-card/42">
          <Sheet open={ouvert} onOpenChange={setOuvert}>
            <SheetTrigger asChild>
              <button
                type="button"
                aria-label="Ouvrir le menu"
                className="inline-flex size-11 items-center justify-center rounded-md hover:bg-accent"
              >
                <MenuIcon className="size-5" />
              </button>
            </SheetTrigger>
            <SheetContent aria-describedby={undefined} className="overflow-hidden border-r-0 bg-card/74 dark:bg-card/42">
              <SheetTitle className="sr-only">Menu</SheetTitle>
              <SheetDescription className="sr-only">Navigation entre les pages</SheetDescription>
              <NavContent onNavigate={() => setOuvert(false)} />
            </SheetContent>
          </Sheet>
          <Marque />
        </header>
        <main className="sur-fond mx-auto max-w-6xl px-4 py-6 md:px-8 md:py-8">{children}</main>
      </div>
    </div>
  );
}

/** Les pages ouvertes par un lien d'accès (ADR 0010) : ni sidebar ni session, un en-tête minimal. */
export function MinimalShell({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen">
      <header className="border-b">
        <div className="mx-auto flex max-w-6xl items-center px-4 py-3">
          <Marque />
        </div>
      </header>
      <main className="mx-auto max-w-6xl px-4 py-6 md:py-8">{children}</main>
    </div>
  );
}
