import { createFileRoute, redirect } from "@tanstack/react-router";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { loginUrl, sessionQuery } from "@/lib/session";

export const Route = createFileRoute("/connexion")({
  validateSearch: (search: Record<string, unknown>): { redirect?: string } =>
    typeof search.redirect === "string" ? { redirect: search.redirect } : {},
  beforeLoad: async ({ context, search }) => {
    const user = await context.queryClient.ensureQueryData(sessionQuery);
    if (user) throw redirect({ href: search.redirect ?? "/" });
  },
  component: Connexion,
});

function Connexion() {
  const { redirect: returnTo } = Route.useSearch();

  return (
    <Card className="mx-auto max-w-md">
      <CardHeader>
        <CardTitle>Connexion</CardTitle>
        <CardDescription>Shameless est réservé aux membres du serveur Discord de la communauté.</CardDescription>
      </CardHeader>
      <CardContent>
        <Button asChild className="w-full">
          <a href={loginUrl(returnTo ?? "/")}>Se connecter avec Discord</a>
        </Button>
      </CardContent>
    </Card>
  );
}
