import { createFileRoute, redirect } from "@tanstack/react-router";

export const Route = createFileRoute("/_connecte/hall-de-la-honte")({
  beforeLoad: () => { throw redirect({ to: "/classements", search: { vue: "dofd" } }); },
});
