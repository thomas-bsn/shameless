import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useLocation, useNavigate } from "@tanstack/react-router";
import { $api } from "@/api/client";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { loginUrl, sessionQuery } from "@/lib/session";

/** L'utilisateur connecté dans l'en-tête, ou le bouton de connexion. */
export function SessionMenu() {
  const session = useQuery(sessionQuery);
  const location = useLocation();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
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
    <div className="flex items-center gap-2">
      <Link to="/compte" className="flex items-center gap-2 rounded-md px-2 py-1 text-sm hover:bg-accent">
        <Avatar className="size-7">
          {user.avatarUrl && <AvatarImage src={user.avatarUrl} alt="" />}
          <AvatarFallback>{user.username?.charAt(0).toUpperCase()}</AvatarFallback>
        </Avatar>
        {user.username}
      </Link>
      <Button variant="ghost" size="sm" disabled={logout.isPending} onClick={() => logout.mutate({})}>
        Déconnexion
      </Button>
    </div>
  );
}
