import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { SelecteurDePeak } from "@/components/selecteur-de-peak";
import { mockApi } from "../api";
import { renderComponent } from "../render";

const saisons = { saisons: ["S24", "S25", "S26"], courante: "S26" };

describe("Sélecteur de peak (rang et saison)", () => {
  it("propose les saisons de S24 à la saison courante, la courante étant choisie d'office", async () => {
    mockApi({ "GET /players/peak/saisons": saisons });
    renderComponent(<SelecteurDePeak onValider={() => {}} />);

    const choix = await screen.findByLabelText("Saison du peak");
    expect(screen.getAllByRole("option", { name: /^S2\d$/ }).map((o) => o.textContent)).toEqual(["S24", "S25", "S26"]);
    expect(choix).toHaveValue("S26");
  });

  it("valide le rang avec la saison choisie", async () => {
    mockApi({ "GET /players/peak/saisons": saisons });
    const onValider = vi.fn();
    renderComponent(<SelecteurDePeak onValider={onValider} />);

    await userEvent.selectOptions(await screen.findByLabelText("Tier du peak"), "EMERALD");
    await userEvent.selectOptions(screen.getByLabelText("Division du peak"), "3");
    await userEvent.selectOptions(screen.getByLabelText("Saison du peak"), "S24");
    await userEvent.clear(screen.getByLabelText("LP du peak"));
    await userEvent.type(screen.getByLabelText("LP du peak"), "12");
    await userEvent.click(screen.getByRole("button", { name: "Enregistrer le peak" }));

    await waitFor(() =>
      expect(onValider).toHaveBeenCalledWith({ peakTier: "EMERALD", peakDivision: 3, peakSeason: "S24", peakLp: 12 }),
    );
  });

  it("n'envoie pas de division pour un rang Master ou plus", async () => {
    mockApi({ "GET /players/peak/saisons": saisons });
    const onValider = vi.fn();
    renderComponent(<SelecteurDePeak onValider={onValider} />);

    await userEvent.selectOptions(await screen.findByLabelText("Tier du peak"), "MASTER");
    expect(screen.getByLabelText("Division du peak")).toBeDisabled();
    await userEvent.click(screen.getByRole("button", { name: "Enregistrer le peak" }));

    await waitFor(() =>
      expect(onValider).toHaveBeenCalledWith({ peakTier: "MASTER", peakDivision: null, peakSeason: "S26", peakLp: 0 }),
    );
  });

  it("part du peak existant quand il y en a un", async () => {
    mockApi({ "GET /players/peak/saisons": saisons });
    renderComponent(
      <SelecteurDePeak
        initial={{ peakTier: "DIAMOND", peakDivision: 2, peakSeason: "S25", peakLp: 40 }}
        onValider={() => {}}
      />,
    );

    expect(await screen.findByLabelText("Saison du peak")).toHaveValue("S25");
    expect(screen.getByLabelText("Tier du peak")).toHaveValue("DIAMOND");
    expect(screen.getByLabelText("Division du peak")).toHaveValue("2");
    expect(screen.getByLabelText("LP du peak")).toHaveValue(40);
  });
});
