import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { shiftMonth } from "@/lib/history";
import { json, mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { base, cleMois, corps, onglet, recap } from "@/test/modifier-game";
import { renderApp } from "@/test/render";

const maintenant = new Date();
const mois = { year: maintenant.getFullYear(), month: maintenant.getMonth() + 1 };
const cle = (m: { year: number; month: number }) => `GET /games/history/${m.year}/${m.month}`;

const historiqueModifie = () => {
  const h = structuredClone((base() as Record<string, unknown>)[cleMois]) as { games: { games: { modifieeParAdminLe: string | null }[] }[] };
  h.games[0].games[0].modifieeParAdminLe = "2026-10-12T09:00:00Z";
  return h;
};

describe("Administration : onglet Games", () => {
  it("liste les games du mois, signale celles modifiées et navigue de mois en mois", async () => {
    mockApi({ ...base(), [cleMois]: historiqueModifie(), [cle(shiftMonth(mois, -1))]: { games: [] } });
    renderApp("/administration?onglet=games");

    const ligne = (await screen.findByRole("button", { name: "Ouvrir la game #42" })).closest("tr") as HTMLElement;
    expect(within(ligne).getByText(/modifiée par un admin le 12\/10\/2026/)).toBeInTheDocument();
    expect(within(ligne).getByText(/Équipe A/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Mois précédent" }));
    expect(await screen.findByText("Aucune game terminée ce mois-ci.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Mois suivant" }));
    expect(await screen.findByRole("button", { name: "Ouvrir la game #42" })).toBeInTheDocument();
  });

  it("ouvre la même fenêtre à onglets que la page de la game", async () => {
    const fetchMock = mockApi({ ...base(), "PUT /games/42/correction": recap({ winner: "B" }) });
    renderApp("/administration?onglet=games");
    await userEvent.click(await screen.findByRole("button", { name: "Ouvrir la game #42" }));
    const dialogue = await screen.findByRole("dialog");

    for (const nom of ["Résultat", "Composition", "Stats", "Rapport", "Supprimer"]) {
      expect(within(dialogue).getByRole("tab", { name: nom })).toBeInTheDocument();
    }
    await userEvent.click(within(dialogue).getByRole("radio", { name: /Équipe B/ }));
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer" }));
    expect(JSON.parse(await corps(fetchMock, "PUT", "/games/42/correction"))).toMatchObject({ winner: "B" });
  });

  it("ferme la fenêtre après la suppression de la game", async () => {
    mockApi({ ...base(), "DELETE /games/42": new Response(null, { status: 204 }) });
    renderApp("/administration?onglet=games");
    await userEvent.click(await screen.findByRole("button", { name: "Ouvrir la game #42" }));
    const dialogue = await screen.findByRole("dialog");
    await onglet(dialogue, "Supprimer");

    await userEvent.click(within(dialogue).getByRole("checkbox", { name: "Je comprends que c'est définitif" }));
    await userEvent.click(within(dialogue).getByRole("button", { name: "Supprimer la game" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("montre « modifiée par un admin » sur la page de la game", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": recap({ modifieeParAdminLe: "2026-10-12T09:00:00Z" }), "GET /games/42/votes": json({}) });
    renderApp("/games/42");

    expect(await screen.findByText(/modifiée par un admin le 12\/10\/2026/)).toBeInTheDocument();
  });
});
