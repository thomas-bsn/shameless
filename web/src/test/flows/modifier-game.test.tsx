import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockApi, problem } from "@/test/api";
import { base, corps, ouvrir, recap } from "@/test/modifier-game";
import { renderApp } from "@/test/render";

describe("Bouton « Modifier la game »", () => {
  it("est visible des Administrateurs sur la page de la game", async () => {
    mockApi(base(true));
    renderApp("/games/42");

    expect(await screen.findByRole("button", { name: "Modifier la game" })).toBeInTheDocument();
  });

  it("n'est pas visible des autres utilisateurs", async () => {
    mockApi(base(false));
    renderApp("/games/42");

    await screen.findByText(/Victoire de l'Équipe A/);
    expect(screen.queryByRole("button", { name: "Modifier la game" })).not.toBeInTheDocument();
  });
});

describe("Fenêtre Modifier la game : onglet Résultat", () => {
  it("montre l'effet d'un changement de vainqueur sur la Série, et le bouton est grisé sans changement", async () => {
    mockApi(base());
    const dialogue = await ouvrir();

    expect(within(dialogue).getByRole("heading", { name: "Modifier la game #42" })).toBeInTheDocument();
    const enregistrer = within(dialogue).getByRole("button", { name: "Enregistrer" });
    expect(enregistrer).toBeDisabled();

    await userEvent.click(within(dialogue).getByRole("radio", { name: /Équipe B/ }));

    expect(enregistrer).toBeEnabled();
    const effet = within(dialogue).getByText("Effet de ce changement").parentElement as HTMLElement;
    expect(effet).toHaveTextContent("Série BO3 2 – 1 → 1 – 2");
    expect(effet).toHaveTextContent("Vainqueur de la Série Équipe A → Équipe B");
    expect(effet).toHaveTextContent("Costs recalculés 10 joueurs");
  });

  it("enregistre le vainqueur et le confirme à côté du bouton", async () => {
    const fetchMock = mockApi({ ...base(), "PUT /games/42/correction": recap({ winner: "B" }) });
    const dialogue = await ouvrir();

    await userEvent.click(within(dialogue).getByRole("radio", { name: /Équipe B/ }));
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer" }));

    expect(JSON.parse(await corps(fetchMock, "PUT", "/games/42/correction"))).toEqual({ winner: "B", teamA: null, teamB: null });
    const pied = within(dialogue).getByRole("button", { name: "Enregistrer" }).parentElement?.parentElement as HTMLElement;
    expect(await within(pied).findByText("Enregistré.")).toBeInTheDocument();
  });

  it("affiche le refus de l'API à côté du bouton, pas en haut de la fenêtre", async () => {
    mockApi({ ...base(), "PUT /games/42/correction": problem(409, "La série est terminée.", "Conflit") });
    const dialogue = await ouvrir();

    await userEvent.click(within(dialogue).getByRole("radio", { name: /Équipe B/ }));
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer" }));

    const alerte = await within(dialogue).findByRole("alert");
    expect(alerte).toHaveTextContent("La série est terminée.");
    const bouton = within(dialogue).getByRole("button", { name: "Enregistrer" });
    expect(bouton.parentElement?.parentElement).toContainElement(alerte);
  });
});
