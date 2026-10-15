import { problemMessage } from "./problem";

describe("problemMessage", () => {
  it("prefers the detail of a ProblemDetails", () => {
    expect(problemMessage({ status: 409, title: "Conflict", detail: "Ce joueur est déjà lié." })).toBe(
      "Ce joueur est déjà lié.",
    );
  });

  it("explains common statuses without detail, ignoring the English title of ASP.NET", () => {
    expect(problemMessage({ status: 400, title: "One or more validation errors occurred." })).toBe(
      "La demande est invalide.",
    );
    expect(problemMessage({ status: 401 })).toBe("Ta session a expiré, reconnecte-toi.");
    expect(problemMessage({ status: 403 })).toBe("Tu n'as pas les droits pour faire ça.");
    expect(problemMessage({ status: 404 })).toBe("Introuvable.");
  });

  it("has a generic message for anything else", () => {
    expect(problemMessage({ status: 500 })).toBe("Une erreur est survenue, réessaie plus tard.");
    expect(problemMessage(new TypeError("Failed to fetch"))).toBe("Une erreur est survenue, réessaie plus tard.");
    expect(problemMessage(undefined)).toBe("Une erreur est survenue, réessaie plus tard.");
  });
});
