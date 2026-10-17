import { FUSEAU_PAR_DEFAUT, validerAdresseBot, validerCleRiot, validerFuseau } from "@/lib/wizard/validation";

describe("validerAdresseBot", () => {
  it("accepte une adresse https d'hôte, avec port éventuel", () => {
    expect(validerAdresseBot("https://bot.example.org")).toEqual({ ok: true, valeur: "https://bot.example.org", avertissements: [] });
    expect(validerAdresseBot("  https://bot.example.org:9443 ")).toMatchObject({ ok: true, valeur: "https://bot.example.org:9443" });
  });

  it("refuse une adresse vide avec le message de l'étape", () => {
    expect(validerAdresseBot("   ")).toEqual({ ok: false, erreur: "Indique l'adresse de ton bot, ou désactive l'intégration." });
  });

  it("donne un message distinct par cas d'erreur", () => {
    const erreurs = [
      "pas une adresse",
      "http://bot.example.org",
      "https://bot.example.org/",
      "https://bot.example.org/api",
      "https://bot.example.org?x=1",
      "https://user:mdp@bot.example.org",
    ].map((a) => {
      const r = validerAdresseBot(a);
      expect(r.ok).toBe(false);
      return r.ok ? "" : r.erreur;
    });
    expect(new Set(erreurs).size).toBe(erreurs.length);
    expect(erreurs[1]).toMatch(/https/);
    expect(erreurs[2]).toMatch(/« \/ » final/);
    expect(erreurs[5]).toMatch(/identifiants/);
    expect(validerAdresseBot("https://bot.example.org#haut")).toMatchObject({ ok: false });
    expect(validerAdresseBot("ftp://bot.example.org")).toMatchObject({ ok: false });
  });

  it("n'écrit jamais les identifiants saisis dans le message", () => {
    const r = validerAdresseBot("https://user:supersecret@bot.example.org");
    expect(JSON.stringify(r)).not.toContain("supersecret");
  });

  it("avertit sans bloquer pour localhost, une IP privée ou un nom de service Docker", () => {
    for (const a of ["https://localhost", "https://127.0.0.1:9000", "https://10.0.0.5", "https://172.20.1.1", "https://192.168.1.10", "https://bot", "https://bot.local"]) {
      const r = validerAdresseBot(a);
      expect(r).toMatchObject({ ok: true });
      expect(r.ok && r.avertissements).toHaveLength(1);
      expect(r.ok && r.avertissements[0]).toMatch(/injoignable depuis le conteneur de l'API/);
    }
  });

  it("tolère http seulement pour localhost ou le réseau privé", () => {
    expect(validerAdresseBot("http://localhost:9000")).toMatchObject({ ok: true, valeur: "http://localhost:9000" });
    expect(validerAdresseBot("http://192.168.1.10:9000")).toMatchObject({ ok: true });
    expect(validerAdresseBot("http://172.32.0.1")).toMatchObject({ ok: false });
  });
});

describe("validerCleRiot", () => {
  it("accepte une clé vide : l'Instance fonctionne sans les fonctions Riot", () => {
    expect(validerCleRiot("")).toEqual({ ok: true, valeur: "" });
    expect(validerCleRiot("  ")).toEqual({ ok: true, valeur: "" });
  });

  it("accepte une clé RGAPI-…", () => {
    const cle = "RGAPI-12345678-abcd-4ef0-9abc-0123456789ab";
    expect(validerCleRiot(` ${cle} `)).toEqual({ ok: true, valeur: cle });
  });

  it("refuse une clé sans préfixe ou mal formée, sans la répéter", () => {
    for (const cle of ["12345678-abcd", "RGAPI-", "RGAPI-abc def", "rgapi-secret-secret"]) {
      const r = validerCleRiot(cle);
      expect(r.ok).toBe(false);
      expect(JSON.stringify(r)).not.toContain(cle);
    }
    expect(validerCleRiot("pas-une-cle")).toMatchObject({ erreur: expect.stringMatching(/RGAPI-/) });
  });
});

describe("validerFuseau", () => {
  it("propose Europe/Paris par défaut", () => {
    expect(FUSEAU_PAR_DEFAUT).toBe("Europe/Paris");
    expect(validerFuseau("")).toEqual({ ok: true, valeur: "Europe/Paris" });
  });

  it("accepte un fuseau IANA et le rend dans sa casse canonique", () => {
    expect(validerFuseau("America/New_York")).toEqual({ ok: true, valeur: "America/New_York" });
    expect(validerFuseau("europe/paris")).toEqual({ ok: true, valeur: "Europe/Paris" });
  });

  it("refuse un fuseau inconnu", () => {
    expect(validerFuseau("Paris")).toMatchObject({ ok: false });
    expect(validerFuseau("UTC+2")).toMatchObject({ ok: false });
  });
});
