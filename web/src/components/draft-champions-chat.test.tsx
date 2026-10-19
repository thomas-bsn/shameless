import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { ChatDeLaDraft } from "@/components/draft-champions-chat";
import { json, mockApi, problem } from "@/test/api";
import { renderComponent } from "@/test/render";

const message = (
  id: number,
  auteur: string,
  contenu: string,
  equipe: "A" | "B" | null = null,
) => ({
  id,
  auteur,
  avatarUrl: null,
  equipe,
  contenu,
  envoyeLe: new Date().toISOString(),
});

describe("Chat de la draft", () => {
  it("affiche les messages de la draft avec leur auteur", async () => {
    mockApi({
      "GET /liens/lien-spec/draft-champions/messages": {
        messages: [
          message(1, "A1", "gl hf", "A"),
          message(2, "Spectateur 1", "allez B"),
        ],
      },
    });
    renderComponent(<ChatDeLaDraft lien="lien-spec" />);

    expect(await screen.findByText("gl hf")).toBeInTheDocument();
    expect(screen.getByText("allez B")).toBeInTheDocument();
    expect(screen.getByText("Spectateur 1")).toBeInTheDocument();
  });

  it("envoie le message saisi avec un identifiant de spectateur stable, puis vide le champ", async () => {
    const corps: { contenu: string; spectateurId: string }[] = [];
    mockApi({
      "GET /liens/lien-spec/draft-champions/messages": { messages: [] },
      "POST /liens/lien-spec/draft-champions/messages": async (request) => {
        corps.push(await request.json());
        return json(message(1, "Spectateur 1", "salut"), 201);
      },
    });
    renderComponent(<ChatDeLaDraft lien="lien-spec" />);

    const champ = await screen.findByLabelText("Message");
    await userEvent.type(champ, "salut");
    await userEvent.click(screen.getByRole("button", { name: "Envoyer" }));
    await userEvent.type(champ, "encore");
    await userEvent.click(screen.getByRole("button", { name: "Envoyer" }));

    expect(corps.map((c) => c.contenu)).toEqual(["salut", "encore"]);
    expect(corps[0]!.spectateurId).toBeTruthy();
    expect(corps[1]!.spectateurId).toBe(corps[0]!.spectateurId);
    expect(champ).toHaveValue("");
  });

  it("affiche le refus de l'API en clair quand le débit est dépassé", async () => {
    mockApi({
      "GET /liens/lien-spec/draft-champions/messages": { messages: [] },
      "POST /liens/lien-spec/draft-champions/messages": problem(
        429,
        "Vous écrivez trop vite : patientez.",
      ),
    });
    renderComponent(<ChatDeLaDraft lien="lien-spec" />);

    await userEvent.type(await screen.findByLabelText("Message"), "spam");
    await userEvent.click(screen.getByRole("button", { name: "Envoyer" }));

    expect(
      await screen.findByText(/Vous écrivez trop vite/),
    ).toBeInTheDocument();
  });
});
