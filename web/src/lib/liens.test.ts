import { lienCapitaineUrl, lienSpectateurUrl } from "@/lib/liens";

describe("liens d'accès", () => {
  it("le Lien capitaine mène à la page capitaine de la Draft, sur l'origine du front", () => {
    expect(lienCapitaineUrl("abc123", "https://shameless.example")).toBe(
      "https://shameless.example/draft/capitaine/abc123",
    );
  });

  it("le Lien spectateur mène à la page spectateur de la Draft", () => {
    expect(lienSpectateurUrl("xyz789", "http://localhost:5173")).toBe("http://localhost:5173/draft/spectateur/xyz789");
  });

  it("encode le secret dans l'URL", () => {
    expect(lienCapitaineUrl("a/b c", "https://s.example")).toBe("https://s.example/draft/capitaine/a%2Fb%20c");
  });

  it("utilise l'origine de la page par défaut", () => {
    expect(lienSpectateurUrl("s1")).toBe(`${window.location.origin}/draft/spectateur/s1`);
  });
});
