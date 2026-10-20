import { within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockApi, problem } from "@/test/api";
import { appelA, base, corps, equipe, NOMS_A, NOMS_B, onglet, ouvrir, participant, recap } from "@/test/modifier-game";

/** Alice a une correction : Companion 3 assistances, valeur affichée 7. */
const aliceCorrigee = () =>
  participant(1, "Alice", {
    champsCorriges: ["assists"],
    statsOrigine: { ...participant(1, "Alice").stats!, assists: 3 },
  });

const partieCorrigee = () => recap({ teamA: [aliceCorrigee(), ...equipe(2, NOMS_A.slice(1))], teamB: equipe(11, NOMS_B) });

const ouvrirLesStats = async (routes = base()) => {
  const fetchMock = mockApi(routes);
  const dialogue = await ouvrir();
  await onglet(dialogue, "Stats");
  return { fetchMock, dialogue };
};

const ouvrirAlice = async (routes: Record<string, unknown> = {}) => {
  const contexte = await ouvrirLesStats({ ...base(), "GET /games/42": partieCorrigee(), ...routes } as ReturnType<typeof base>);
  await userEvent.click(within(contexte.dialogue).getByRole("button", { name: "Stats de Alice" }));
  return contexte;
};

describe("Fenêtre Modifier la game : onglet Stats, liste", () => {
  it("liste les dix joueurs et signale ceux dont une stat est corrigée", async () => {
    const { dialogue } = await ouvrirLesStats({ ...base(), "GET /games/42": partieCorrigee() });

    for (const prenom of [...NOMS_A, ...NOMS_B]) {
      expect(within(dialogue).getByRole("button", { name: `Stats de ${prenom}` })).toBeInTheDocument();
    }
    const alice = within(dialogue).getByRole("button", { name: "Stats de Alice" });
    expect(within(alice).getByText("corrigée")).toBeInTheDocument();
    expect(within(within(dialogue).getByRole("button", { name: "Stats de Bob" })).queryByText("corrigée")).not.toBeInTheDocument();
  });
});

describe("Fenêtre Modifier la game : onglet Stats, un joueur", () => {
  it("montre les sections, la valeur du Companion sous chaque case et entoure les cases changées", async () => {
    const { dialogue } = await ouvrirAlice();

    for (const section of ["Identité", "Combat", "Farm et or", "Dégâts", "Vision", "Objectifs", "Objets", "Runes", "Note"]) {
      expect(within(dialogue).getByRole("heading", { name: section })).toBeInTheDocument();
    }
    const assistances = within(dialogue).getByLabelText("Assistances de Alice");
    expect(assistances).toHaveValue(7);
    const case_ = assistances.closest("[data-champ]") as HTMLElement;
    expect(case_).toHaveTextContent("Companion : 3");
    expect(case_).toHaveAttribute("data-modifie", "true");
    expect(within(dialogue).getByLabelText("Kills de Alice").closest("[data-champ]")).toHaveAttribute("data-modifie", "false");
  });

  it("grise le bouton sans changement, puis n'envoie que les champs changés", async () => {
    const { dialogue, fetchMock } = await ouvrirAlice({ "PUT /games/42/players/1/corrections": recap() });
    const enregistrer = within(dialogue).getByRole("button", { name: "Enregistrer ce joueur" });
    expect(enregistrer).toBeDisabled();

    const kills = within(dialogue).getByLabelText("Kills de Alice");
    await userEvent.clear(kills);
    await userEvent.type(kills, "12");
    expect(kills.closest("[data-champ]")).toHaveAttribute("data-modifie", "true");
    expect(enregistrer).toBeEnabled();
    await userEvent.click(enregistrer);

    const corpsEnvoye = JSON.parse(await corps(fetchMock, "PUT", "/games/42/players/1/corrections"));
    expect(corpsEnvoye).toMatchObject({ kills: 12, deaths: null, assists: null, champion: null });
    expect(Object.values(corpsEnvoye).filter((v) => v !== null)).toHaveLength(1);
    expect(await within(dialogue).findByText("Enregistré.")).toBeInTheDocument();
  });

  it("affiche le refus de l'API à côté du bouton d'enregistrement", async () => {
    const { dialogue } = await ouvrirAlice({ "PUT /games/42/players/1/corrections": problem(400, "Le champ « kills » va de 0 à 1000000.", "Invalide") });

    const kills = within(dialogue).getByLabelText("Kills de Alice");
    await userEvent.clear(kills);
    await userEvent.type(kills, "12");
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer ce joueur" }));

    const alerte = await within(dialogue).findByRole("alert");
    expect(alerte).toHaveTextContent("va de 0 à 1000000");
    const bouton = within(dialogue).getByRole("button", { name: "Enregistrer ce joueur" });
    expect(bouton.parentElement?.parentElement).toContainElement(alerte);
  });

  it("rétablit un champ corrigé en annulant sa correction", async () => {
    const { dialogue, fetchMock } = await ouvrirAlice({ "DELETE /games/42/players/1/corrections/assists": recap() });

    await userEvent.click(within(dialogue).getByRole("button", { name: "Rétablir Assistances de Alice" }));

    await corps(fetchMock, "DELETE", "/games/42/players/1/corrections/assists");
  });

  it("rétablit un champ changé mais pas enregistré sans appeler l'API", async () => {
    const { dialogue, fetchMock } = await ouvrirAlice();
    const kills = within(dialogue).getByLabelText("Kills de Alice");
    await userEvent.clear(kills);
    await userEvent.type(kills, "12");

    await userEvent.click(within(dialogue).getByRole("button", { name: "Rétablir Kills de Alice" }));

    expect(kills).toHaveValue(5);
    expect(fetchMock.mock.calls.some(([r]) => r.method === "DELETE")).toBe(false);
  });

  it("rétablit toutes les valeurs du Companion du joueur", async () => {
    const aliceDeuxCorrections = participant(1, "Alice", {
      champsCorriges: ["assists", "kills"],
      statsOrigine: { ...participant(1, "Alice").stats!, assists: 3, kills: 1 },
    });
    const { dialogue, fetchMock } = await ouvrirAlice({
      "GET /games/42": recap({ teamA: [aliceDeuxCorrections, ...equipe(2, NOMS_A.slice(1))] }),
      "DELETE /games/42/players/1/corrections/assists": recap(),
      "DELETE /games/42/players/1/corrections/kills": recap(),
    });

    await userEvent.click(within(dialogue).getByRole("button", { name: "Rétablir les valeurs du Companion" }));

    await corps(fetchMock, "DELETE", "/games/42/players/1/corrections/assists");
    await corps(fetchMock, "DELETE", "/games/42/players/1/corrections/kills");
    expect(appelA(fetchMock, "PUT", "/games/42/players/1/corrections")).toBeUndefined();
  });

  it("passe d'un joueur au suivant et au précédent, et revient à la liste", async () => {
    const { dialogue } = await ouvrirAlice();
    expect(within(dialogue).getByText(/joueur 1 sur 10/)).toBeInTheDocument();
    expect(within(dialogue).getByRole("button", { name: "Joueur précédent" })).toBeDisabled();

    await userEvent.click(within(dialogue).getByRole("button", { name: "Joueur suivant" }));
    expect(within(dialogue).getByText(/joueur 2 sur 10/)).toBeInTheDocument();
    expect(within(dialogue).getByLabelText("Kills de Bob")).toBeInTheDocument();

    await userEvent.click(within(dialogue).getByRole("button", { name: "Joueur précédent" }));
    expect(within(dialogue).getByLabelText("Kills de Alice")).toBeInTheDocument();

    await userEvent.click(within(dialogue).getByRole("button", { name: "Retour à la liste" }));
    expect(within(dialogue).getByRole("button", { name: "Stats de Bob" })).toBeInTheDocument();
  });

  it("garde ce qui est saisi en changeant de joueur", async () => {
    const { dialogue } = await ouvrirAlice();
    const kills = within(dialogue).getByLabelText("Kills de Alice");
    await userEvent.clear(kills);
    await userEvent.type(kills, "12");

    await userEvent.click(within(dialogue).getByRole("button", { name: "Joueur suivant" }));
    await userEvent.click(within(dialogue).getByRole("button", { name: "Joueur précédent" }));

    expect(within(dialogue).getByLabelText("Kills de Alice")).toHaveValue(12);
  });

  it("cherche une stat par son nom", async () => {
    const { dialogue } = await ouvrirAlice();

    await userEvent.type(within(dialogue).getByRole("searchbox", { name: "Chercher une stat" }), "vision");

    expect(within(dialogue).getByLabelText("Score de vision de Alice")).toBeInTheDocument();
    expect(within(dialogue).queryByLabelText("Kills de Alice")).not.toBeInTheDocument();

    await userEvent.clear(within(dialogue).getByRole("searchbox", { name: "Chercher une stat" }));
    await userEvent.type(within(dialogue).getByRole("searchbox", { name: "Chercher une stat" }), "zzz");
    expect(within(dialogue).getByText("Aucune stat ne correspond.")).toBeInTheDocument();
  });

  it("permet de corriger la note et le champion", async () => {
    const { dialogue, fetchMock } = await ouvrirAlice({ "PUT /games/42/players/1/corrections": recap() });

    const note = within(dialogue).getByLabelText("Note de Alice");
    await userEvent.clear(note);
    await userEvent.type(note, "88");
    const champion = within(dialogue).getByLabelText("Champion de Alice");
    await userEvent.clear(champion);
    await userEvent.type(champion, "Zed");
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer ce joueur" }));

    expect(JSON.parse(await corps(fetchMock, "PUT", "/games/42/players/1/corrections"))).toMatchObject({ note: 88, champion: "Zed", kills: null });
  });
});

describe("Fenêtre Modifier la game : onglet Stats, objets, sorts et runes", () => {
  it("choisit les objets, les sorts et les runes par leur nom, avec leur icône", async () => {
    const { dialogue, fetchMock } = await ouvrirAlice({ "PUT /games/42/players/1/corrections": recap() });

    const objet1 = within(dialogue).getByLabelText("Objet 1 de Alice");
    expect(objet1).toHaveDisplayValue("Coiffe de Rabadon");
    expect(objet1.closest("[data-champ]")?.querySelector("img")).toHaveAttribute("src", "https://ddragon.test/item/3089.png");
    expect(within(dialogue).getByLabelText("Sort 1 de Alice")).toHaveDisplayValue("Saut éclair");
    expect(within(dialogue).getByLabelText("Rune 1 de Alice")).toHaveDisplayValue("Électrocution");
    expect(within(dialogue).getByLabelText("Arbre principal de Alice")).toBeInTheDocument();

    await userEvent.selectOptions(within(dialogue).getByLabelText("Objet 2 de Alice"), "Sablier de Zhonya");
    await userEvent.selectOptions(within(dialogue).getByLabelText("Rune 2 de Alice"), "Transcendance");
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer ce joueur" }));

    const corpsEnvoye = JSON.parse(await corps(fetchMock, "PUT", "/games/42/players/1/corrections"));
    expect(corpsEnvoye).toMatchObject({ item1: 3157, perk1: 8210, item0: null, perk0: null });
  });

  it("ne propose aucun fragment de rune", async () => {
    const { dialogue } = await ouvrirAlice();

    expect(within(dialogue).queryByText(/fragment/i)).not.toBeInTheDocument();
    expect(within(dialogue).getAllByLabelText(/^Rune \d de Alice$/)).toHaveLength(6);
  });

  it("retombe sur des champs d'identifiants numériques quand le catalogue est indisponible", async () => {
    const { dialogue, fetchMock } = await ouvrirAlice({
      "GET /catalogue": problem(503, "Le catalogue n'est pas disponible."),
      "PUT /games/42/players/1/corrections": recap(),
    });

    const objet1 = await within(dialogue).findByLabelText("Objet 1 de Alice");
    expect(objet1).toHaveValue(3089);
    expect(objet1.tagName).toBe("INPUT");
    await userEvent.clear(objet1);
    await userEvent.type(objet1, "3157");
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer ce joueur" }));

    expect(JSON.parse(await corps(fetchMock, "PUT", "/games/42/players/1/corrections"))).toMatchObject({ item0: 3157 });
  });
});
