import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { json, mockApi } from "@/test/api";
import { renderComponent } from "@/test/render";
import { CodeCompanionPanel } from "./code-companion-panel";

describe("CodeCompanionPanel", () => {
  it("affiche le code Companion de la Série et permet de le copier", async () => {
    mockApi({ "GET /series/5/code-companion": { seriesId: 5, code: "code-secret-1" } });
    const writeText = vi.fn().mockResolvedValue(undefined);
    const user = userEvent.setup();
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });

    renderComponent(<CodeCompanionPanel seriesId={5} />);

    expect(await screen.findByLabelText("Code Companion")).toHaveValue("code-secret-1");
    await user.click(screen.getByRole("button", { name: "Copier" }));
    expect(writeText).toHaveBeenCalledWith("code-secret-1");
    expect(await screen.findByRole("button", { name: "Copié" })).toBeInTheDocument();
  });

  it("renouvelle le code après confirmation et affiche le nouveau", async () => {
    const renouveler = vi.fn(() => json({ seriesId: 5, code: "code-secret-2" }));
    mockApi({
      "GET /series/5/code-companion": { seriesId: 5, code: "code-secret-1" },
      "POST /series/5/code-companion/renouveler": renouveler,
    });
    vi.spyOn(window, "confirm").mockReturnValue(true);
    const user = userEvent.setup();

    renderComponent(<CodeCompanionPanel seriesId={5} />);
    await screen.findByLabelText("Code Companion");
    await user.click(screen.getByRole("button", { name: "Renouveler" }));

    expect(await screen.findByDisplayValue("code-secret-2")).toBeInTheDocument();
    expect(renouveler).toHaveBeenCalledOnce();
  });
});
