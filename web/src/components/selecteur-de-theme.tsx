import { Moon, Sun } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useTheme, type Theme } from "@/lib/theme";

const choix: { theme: Theme; label: string; icone: typeof Moon }[] = [
  { theme: "dark", label: "Sombre", icone: Moon },
  { theme: "light", label: "Clair", icone: Sun },
];

/** Le sélecteur de thème de la sidebar : le choix est mémorisé dans le navigateur. */
export function SelecteurDeTheme() {
  const [theme, definirTheme] = useTheme();

  return (
    <div role="group" aria-label="Thème" className="flex gap-1 rounded-md border p-1">
      {choix.map(({ theme: valeur, label, icone: Icone }) => (
        <Button
          key={valeur}
          variant={theme === valeur ? "secondary" : "ghost"}
          size="sm"
          className="flex-1"
          aria-pressed={theme === valeur}
          onClick={() => definirTheme(valeur)}
        >
          <Icone aria-hidden />
          {label}
        </Button>
      ))}
    </div>
  );
}
