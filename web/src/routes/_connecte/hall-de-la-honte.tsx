import { createFileRoute, redirect } from "@tanstack/react-router";

/** L'ancien Hall de la Honte est devenu la vue DOFD de Classements. */
export const Route = createFileRoute("/_connecte/hall-de-la-honte")({
  beforeLoad: () => {
    throw redirect({ to: "/classements", search: { vue: "dofd" }, replace: true });
  },
});
