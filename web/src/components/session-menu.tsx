import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useLocation, useNavigate } from "@tanstack/react-router";
import { ChevronsUpDownIcon } from "lucide-react";
import { $api } from "@/api/client";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { loginUrl, sessionQuery } from "@/lib/session";
import { useTheme } from "@/lib/theme";

/** Le menu de l'utilisateur connecté (profil, thème, déconnexion), ou le bouton de connexion. */
export function SessionMenu() {
  const session = useQuery(sessionQuery);
  const location = useLocation();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [theme, setTheme] = useTheme();
  const logout = $api.useMutation("post", "/auth/logout", {
    onSuccess: async () => {
      queryClient.removeQueries();
      queryClient.setQueryData(sessionQuery.queryKey, null);
      await navigate({ to: "/" });
    },
  });

  if (session.isPending) return null;

  const user = session.data;
  if (!user) {
    return (
      <Button asChild size="sm">
        <a href={loginUrl(location.href)}>Se connecter</a>
      </Button>
    );
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          className="flex w-full min-w-0 items-center gap-2 rounded-lg p-2 text-left text-sm hover:bg-accent data-[state=open]:bg-accent"
        >
          <Avatar className="size-8">
            {user.avatarUrl && <AvatarImage src={user.avatarUrl} alt="" />}
            <AvatarFallback>{user.username?.charAt(0).toUpperCase()}</AvatarFallback>
          </Avatar>
          <span className="min-w-0 flex-1 truncate font-semibold">{user.username}</span>
          <ChevronsUpDownIcon className="size-4 shrink-0 text-muted-foreground" />
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent side="top" align="start" className="w-(--radix-dropdown-menu-trigger-width)">
        <DropdownMenuItem asChild>
          <Link to="/compte">Mon profil</Link>
        </DropdownMenuItem>
        {/* Un clic bascule le thème ; le menu reste ouvert et montre le nouveau réglage. */}
        <DropdownMenuItem
          onSelect={(event) => {
            event.preventDefault();
            setTheme(theme === "dark" ? "light" : "dark");
          }}
        >
          Thème{" "}
          <span className="ml-auto text-xs text-muted-foreground">{theme === "dark" ? "Sombre" : "Clair"}</span>
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive" disabled={logout.isPending} onSelect={() => logout.mutate({})}>
          Se déconnecter
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
