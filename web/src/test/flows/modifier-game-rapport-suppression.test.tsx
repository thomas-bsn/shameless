import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { json, mockApi, problem } from "@/test/api";
import { appelA, base, corps, onglet, ouvrir, ouvrirAvecRouteur, recap } from "@/test/modifier-game";

const deposer = async (dialogue: HTMLElement, contenu: string) => {
  await userEvent.upload(within(dialogue).getByLabelText("Fichier de Rapport"), new File([contenu], "rapport.json", { type: "application/json" }));
  await userEvent.click(within(dialogue).getByRole("button", { name: "Importer le rapport" }));
};

describe("Fenêtre Modifier la game : onglet Rapport", () => {
  it("importe un Fichier de Rapport et le confirme à côté du bouton", async () => {
    const fichier = { riotMatchId: "EUW1_1", donnees: {}, joueurs: [{ puuid: "p1", side: "blue", championId: 64, victoire: true, stats: {} }] };
    const fetchMock = mockApi({ ...base(), "POST /games/42/import-rapport": recap({ riotMatchId: "EUW1_1" }) });
    const dialogue = await ouvrir();
    await onglet(dialogue, "Rapport");

    expect(within(dialogue).getByRole("button", { name: "Importer le rapport" })).toBeDisabled();
    await deposer(dialogue, JSON.stringify(fichier));

    expect(JSON.parse(await corps(fetchMock, "POST", "/games/42/import-rapport"))).toEqual(fichier);
    expect(await within(dialogue).findByText("Rapport importé.")).toBeInTheDocument();
  });

  it("regroupe les écarts d'un import refusé : participants absents, joueurs inconnus, Sides, vainqueur", async () => {
    mockApi({
      ...base(),
      "POST /games/42/import-rapport": json(
        {
          status: 409,
          title: "Import refusé",
          detail: "Le fichier ne correspond pas à la game : rien n'a été importé.",
          ecarts: [
            "Alice est participant de la game mais absent du fichier.",
            "Bob est participant de la game mais absent du fichier.",
            "Le joueur Zed#EUW est dans le fichier mais n'a pas joué cette game.",
            "Chloé a joué le Side Blue dans la game, mais Red dans le fichier.",
            "Le vainqueur diffère : l'Équipe A a gagné la game, le fichier donne la victoire au Side Red.",
          ],
        },
        409,
      ),
    });
    const dialogue = await ouvrir();
    await onglet(dialogue, "Rapport");

    await deposer(dialogue, "{}");

    expect(await within(dialogue).findByText(/Import refusé : rien n'a été importé/)).toBeInTheDocument();
    const absents = within(dialogue).getByText("Participants absents du fichier").parentElement as HTMLElement;
    expect(absents).toHaveTextContent("2 · Alice, Bob");
    expect(within(dialogue).getByText("Joueurs du fichier absents de la game").parentElement).toHaveTextContent("1 · Zed#EUW");
    expect(within(dialogue).getByText("Sides différents").parentElement).toHaveTextContent("1 · Chloé");
    expect(within(dialogue).getByText("Vainqueur différent").parentElement).toHaveTextContent("oui");
    expect(within(dialogue).queryByText("Rapport importé.")).not.toBeInTheDocument();

    await userEvent.click(within(dialogue).getByRole("button", { name: "Voir les 5 écarts" }));
    expect(within(dialogue).getByText("Alice est participant de la game mais absent du fichier.")).toBeInTheDocument();
  });

  it("refuse un fichier qui n'est pas du JSON sans appeler l'API", async () => {
    const fetchMock = mockApi({ ...base(), "POST /games/42/import-rapport": recap() });
    const dialogue = await ouvrir();
    await onglet(dialogue, "Rapport");

    await deposer(dialogue, "pas du json");

    expect(await within(dialogue).findByText(/n'est pas un fichier JSON/)).toBeInTheDocument();
    expect(appelA(fetchMock, "POST", "/games/42/import-rapport")).toBeUndefined();
  });

  it("affiche un autre refus de l'API à côté du bouton", async () => {
    mockApi({ ...base(), "POST /games/42/import-rapport": problem(400, "Ce rapport est invalide.", "Invalide") });
    const dialogue = await ouvrir();
    await onglet(dialogue, "Rapport");

    await deposer(dialogue, "{}");

    expect(await within(dialogue).findByRole("alert")).toHaveTextContent("Ce rapport est invalide.");
  });
});

describe("Fenêtre Modifier la game : onglet Supprimer", () => {
  it("dit ce qui sera recalculé et exige la case « Je comprends » avant de supprimer", async () => {
    const fetchMock = mockApi({ ...base(), "DELETE /games/42": new Response(null, { status: 204 }) });
    const dialogue = await ouvrir();
    await onglet(dialogue, "Supprimer");

    expect(within(dialogue).getByText("Ce qui sera recalculé").parentElement).toHaveTextContent("Série BO3 2 – 1 → 1 – 1");
    expect(within(dialogue).getByText("Ce qui sera recalculé").parentElement).toHaveTextContent("Costs du mois 10 joueurs");
    const supprimer = within(dialogue).getByRole("button", { name: "Supprimer la game" });
    expect(supprimer).toBeDisabled();

    await userEvent.click(within(dialogue).getByRole("checkbox", { name: "Je comprends que c'est définitif" }));
    expect(supprimer).toBeEnabled();
    expect(appelA(fetchMock, "DELETE", "/games/42")).toBeUndefined();
    await userEvent.click(supprimer);

    await corps(fetchMock, "DELETE", "/games/42");
  });

  it("annonce qu'une Série vidée est effacée", async () => {
    mockApi({ ...base(), "GET /games/42": recap({ bestOf: 1, teamAWins: 1, teamBWins: 0 }) });
    const dialogue = await ouvrir();
    await onglet(dialogue, "Supprimer");

    expect(within(dialogue).getByText("Ce qui sera recalculé").parentElement).toHaveTextContent(/Série.*effacée/);
  });

  it("ramène à l'Historique après la suppression depuis la page de la game", async () => {
    mockApi({ ...base(), "DELETE /games/42": new Response(null, { status: 204 }) });
    const { dialogue, router } = await ouvrirAvecRouteur();
    await onglet(dialogue, "Supprimer");
    await userEvent.click(within(dialogue).getByRole("checkbox", { name: "Je comprends que c'est définitif" }));
    await userEvent.click(within(dialogue).getByRole("button", { name: "Supprimer la game" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/historique"));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("affiche le refus de l'API à côté du bouton", async () => {
    mockApi({ ...base(), "DELETE /games/42": problem(409, "La game est utilisée.", "Conflit") });
    const dialogue = await ouvrir();
    await onglet(dialogue, "Supprimer");
    await userEvent.click(within(dialogue).getByRole("checkbox", { name: "Je comprends que c'est définitif" }));
    await userEvent.click(within(dialogue).getByRole("button", { name: "Supprimer la game" }));

    const alerte = await within(dialogue).findByRole("alert");
    expect(alerte).toHaveTextContent("La game est utilisée.");
    expect(within(dialogue).getByRole("button", { name: "Supprimer la game" }).parentElement?.parentElement).toContainElement(alerte);
  });
});
