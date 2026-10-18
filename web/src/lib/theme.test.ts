import { appliquerTheme, lireTheme, THEME_STORAGE_KEY } from "./theme";

describe("Thème", () => {
  afterEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove("dark");
  });

  it("est sombre par défaut, sans choix mémorisé", () => {
    expect(lireTheme()).toBe("dark");
  });

  it("relit le choix mémorisé dans le navigateur", () => {
    localStorage.setItem(THEME_STORAGE_KEY, "light");

    expect(lireTheme()).toBe("light");
  });

  it("ignore un choix mémorisé inconnu", () => {
    localStorage.setItem(THEME_STORAGE_KEY, "fluo");

    expect(lireTheme()).toBe("dark");
  });

  it("applique le thème clair en retirant la classe sombre de <html>", () => {
    appliquerTheme("light");

    expect(document.documentElement).not.toHaveClass("dark");
  });

  it("applique le thème sombre en posant la classe sombre sur <html>", () => {
    appliquerTheme("light");
    appliquerTheme("dark");

    expect(document.documentElement).toHaveClass("dark");
  });
});
