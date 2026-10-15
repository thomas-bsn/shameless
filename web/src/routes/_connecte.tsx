import { createFileRoute, Outlet, redirect } from "@tanstack/react-router";
import { sessionQuery } from "@/lib/session";

/** Toutes les routes sous ce layout demandent une session ; sinon, direction la connexion. */
export const Route = createFileRoute("/_connecte")({
  beforeLoad: async ({ context, location }) => {
    const user = await context.queryClient.ensureQueryData(sessionQuery);
    if (!user) throw redirect({ to: "/connexion", search: { redirect: location.href } });
    return { user };
  },
  component: Outlet,
});
