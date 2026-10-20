import { fireEvent, screen } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi, problem } from "@/test/api";
import { renderComponent } from "@/test/render";
import { AnneauDeNote } from "./anneau-de-note";
import { BadgeDeTitre } from "./badge-de-titre";
import { IconeCapacite, IconeChampion, IconeObjet, IconeRune, IconeSort } from "./icones-catalogue";
import { IconeObjectif, type Objectif } from "./icone-objectif";
import { IconePing, type Ping } from "./icone-ping";
import { IconeRole, type Role } from "./icone-role";

const catalogue: Schemas["CatalogueDeJeu"] = {
  version: "15.1.1",
  objets: [{ id: 6655, name: "Rayon de Luden", iconUrl: "https://dd/item/6655.png" }],
  sorts: [{ id: 4, name: "Saut éclair", iconUrl: "https://dd/spell/Flash.png" }],
  arbres: [
    {
      id: 8100,
      name: "Domination",
      iconUrl: "https://dd/perk/8100.png",
      runes: [{ id: 8112, name: "Électrocution", iconUrl: "https://dd/perk/8112.png" }],
    },
  ],
  champions: [
    {
      id: "LeeSin",
      capacites: [{ touche: "Q", name: "Onde sonore", iconUrl: "https://dd/spell/BlindMonkQOne.png" }],
    },
  ],
};

const champions: Schemas["ChampionResponse"][] = [
  { id: "LeeSin", key: "64", name: "Lee Sin", imageUrl: "https://dd/champ/LeeSin.png", voies: [] },
];

describe("icônes du catalogue", () => {
  beforeEach(() => {
    mockApi({ "GET /catalogue": catalogue, "GET /champions": champions });
  });

  it("montre l'icône d'un champion, d'un sort, d'un objet, d'une rune et d'une capacité connus", async () => {
    renderComponent(
      <>
        <IconeChampion championId="LeeSin" />
        <IconeSort sortId={4} />
        <IconeObjet objetId={6655} />
        <IconeRune runeId={8112} />
        <IconeCapacite championId="LeeSin" touche="Q" />
      </>,
    );

    expect(await screen.findByRole("img", { name: "Lee Sin" })).toHaveAttribute("src", "https://dd/champ/LeeSin.png");
    expect(await screen.findByRole("img", { name: "Saut éclair" })).toHaveAttribute("src", "https://dd/spell/Flash.png");
    expect(await screen.findByRole("img", { name: "Rayon de Luden" })).toHaveAttribute("src", "https://dd/item/6655.png");
    expect(await screen.findByRole("img", { name: "Électrocution" })).toHaveAttribute("src", "https://dd/perk/8112.png");
    expect(await screen.findByRole("img", { name: "Q : Onde sonore" })).toHaveAttribute(
      "src",
      "https://dd/spell/BlindMonkQOne.png",
    );
  });

  it("montre l'arbre de runes quand l'id est celui d'un arbre", async () => {
    renderComponent(<IconeRune runeId={8100} />);

    expect(await screen.findByRole("img", { name: "Domination" })).toBeInTheDocument();
  });

  it("montre un emplacement neutre pour un id inconnu ou absent", async () => {
    renderComponent(
      <>
        <IconeChampion championId="Inconnu" />
        <IconeSort sortId={999} />
        <IconeObjet objetId={null} />
        <IconeRune runeId={1} />
        <IconeCapacite championId="LeeSin" touche="R" />
      </>,
    );

    expect(await screen.findAllByRole("img", { name: "Inconnu" })).toHaveLength(5);
  });
});

describe("icônes du catalogue indisponible", () => {
  it("montre un emplacement neutre quand le catalogue répond 503", async () => {
    mockApi({ "GET /catalogue": problem(503), "GET /champions": champions });

    renderComponent(
      <>
        <IconeObjet objetId={6655} />
        <IconeCapacite championId="LeeSin" touche="Q" />
      </>,
    );

    expect(await screen.findAllByRole("img", { name: "Inconnu" })).toHaveLength(2);
  });
});

describe("icônes SVG", () => {
  it.each<[Role, string, string]>([
    ["Top", "Top", "top"],
    ["Jungle", "Jungle", "jungle"],
    ["Mid", "Mid", "middle"],
    ["Adc", "ADC", "bottom"],
    ["Support", "Support", "utility"],
  ])("l'icône du rôle %s est l'image officielle de Riot, nommée %s", async (role, nom, fichier) => {
    renderComponent(<IconeRole role={role} />);

    expect(await screen.findByRole("img", { name: nom })).toHaveAttribute(
      "src",
      `https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-static-assets/global/default/svg/position-${fichier}.svg`,
    );
  });

  it("l'icône d'un rôle se replie sur l'emplacement neutre quand l'image ne charge pas", async () => {
    renderComponent(<IconeRole role="Top" />);

    fireEvent.error(await screen.findByRole("img", { name: "Top" }));

    expect(await screen.findByRole("img", { name: "Inconnu" })).toBeInTheDocument();
    expect(screen.queryByRole("img", { name: "Top" })).not.toBeInTheDocument();
  });

  it("l'icône d'un rôle inconnu est l'emplacement neutre", async () => {
    renderComponent(<IconeRole role={"Autre" as Role} />);

    expect(await screen.findByRole("img", { name: "Inconnu" })).toBeInTheDocument();
  });

  it.each<[Objectif, string]>([
    ["tourelle", "Tourelle"],
    ["dragon", "Dragon"],
    ["baron", "Baron"],
    ["heraut", "Héraut"],
    ["larve", "Larve du Néant"],
    ["inhibiteur", "Inhibiteur"],
  ])("l'icône d'objectif %s se nomme %s", async (objectif, nom) => {
    renderComponent(<IconeObjectif objectif={objectif} />);

    expect(await screen.findByRole("img", { name: nom })).toBeInTheDocument();
  });

  it.each<[Ping, string]>([
    ["allIn", "Tous en avant"],
    ["danger", "Danger"],
    ["onMyWay", "En route"],
    ["visionCleared", "Zone dégagée"],
  ])("l'icône du ping %s se nomme %s", async (ping, nom) => {
    renderComponent(<IconePing ping={ping} />);

    expect(await screen.findByRole("img", { name: nom })).toBeInTheDocument();
  });
});

describe("AnneauDeNote", () => {
  it.each([
    [100, "or"],
    [90, "or"],
    [89, "vert"],
    [75, "vert"],
    [74, "bleu"],
    [50, "bleu"],
    [49, "orange"],
    [30, "orange"],
    [29, "rouge"],
    [0, "rouge"],
  ])("la note %i est dans le palier %s", async (note, palier) => {
    renderComponent(<AnneauDeNote note={note} />);

    expect(await screen.findByRole("img", { name: `Note ${note} sur 100` })).toHaveAttribute("data-palier", palier);
  });

  it("affiche le chiffre de la note", async () => {
    renderComponent(<AnneauDeNote note={87} />);

    expect(await screen.findByText("87")).toBeInTheDocument();
  });
});

describe("BadgeDeTitre", () => {
  it.each(["MVP", "ACE", "DOFD"] as const)("rend le badge %s", async (titre) => {
    renderComponent(<BadgeDeTitre titre={titre} />);

    expect(await screen.findByText(titre)).toBeInTheDocument();
  });
});
