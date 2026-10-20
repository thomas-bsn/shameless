import { within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockApi, problem } from "@/test/api";
import { base, corps, onglet, ouvrir, participant, recap } from "@/test/modifier-game";

describe("Fenêtre Modifier la game : onglet Composition", () => {
  it("grise les joueurs déjà placés, dans l'autre équipe comme dans la même", async () => {
    mockApi(base());
    const dialogue = await ouvrir();
    await onglet(dialogue, "Composition");

    const liste = within(dialogue).getByLabelText("Équipe B, joueur 1");
    expect(within(liste).getByRole("option", { name: /Alice.*déjà en équipe A/ })).toBeDisabled();
    expect(within(liste).getByRole("option", { name: /Gaëlle.*déjà en équipe B/ })).toBeDisabled();
    expect(within(liste).getByRole("option", { name: "Farid" })).toBeEnabled();
    expect(within(liste).getByRole("option", { name: "Kevin" })).toBeEnabled();
  });

  it("garde les lignes dans l'ordre Top, Jungle, Mid, ADC, Support même si l'équipe arrive dans le désordre", async () => {
    const poste = (id: number, prenom: string, position: string) =>
      participant(id, prenom, { stats: { ...participant(id, prenom).stats!, position } });
    const desordre = [poste(1, "Alice", "Top"), poste(2, "Bob", "Jungle"), poste(3, "Chloé", "Support"), poste(4, "David", "Mid"), poste(5, "Emma", "Adc")];
    mockApi({ ...base(), "GET /games/42": recap({ teamA: desordre }) });
    const dialogue = await ouvrir();
    await onglet(dialogue, "Composition");

    const listes = [1, 2, 3, 4, 5].map((n) => within(dialogue).getByLabelText(`Équipe A, joueur ${n}`) as HTMLSelectElement);
    expect(listes.map((l) => l.selectedOptions[0]!.textContent)).toEqual(["Alice", "Bob", "David", "Emma", "Chloé"]);
    expect(listes.map((l) => l.previousElementSibling?.textContent)).toEqual(["Top", "Jungle", "Mid", "ADC", "Support"]);
  });

  it("enregistre une composition modifiée, le bouton étant grisé sans changement", async () => {
    const fetchMock = mockApi({ ...base(), "PUT /games/42/correction": recap() });
    const dialogue = await ouvrir();
    await onglet(dialogue, "Composition");

    const enregistrer = within(dialogue).getByRole("button", { name: "Enregistrer la composition" });
    expect(enregistrer).toBeDisabled();
    await userEvent.selectOptions(within(dialogue).getByLabelText("Équipe A, joueur 5"), "Kevin");
    expect(enregistrer).toBeEnabled();
    await userEvent.click(enregistrer);

    expect(JSON.parse(await corps(fetchMock, "PUT", "/games/42/correction"))).toEqual({
      winner: null,
      teamA: [1, 2, 3, 4, 99],
      teamB: [11, 12, 13, 14, 15],
    });
    expect(await within(dialogue).findByText("Enregistré.")).toBeInTheDocument();
  });

  it("affiche le refus de l'API à côté du bouton", async () => {
    mockApi({ ...base(), "PUT /games/42/correction": problem(409, "La série est terminée.", "Conflit") });
    const dialogue = await ouvrir();
    await onglet(dialogue, "Composition");

    await userEvent.selectOptions(within(dialogue).getByLabelText("Équipe A, joueur 5"), "Kevin");
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer la composition" }));

    const alerte = await within(dialogue).findByRole("alert");
    expect(alerte).toHaveTextContent("La série est terminée.");
    const bouton = within(dialogue).getByRole("button", { name: "Enregistrer la composition" });
    expect(bouton.parentElement?.parentElement).toContainElement(alerte);
  });
});
