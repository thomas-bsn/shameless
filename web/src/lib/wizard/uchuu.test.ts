import { construireRecapUchuu } from "@/lib/wizard/uchuu";

const secrets = { botApiKey: "a".repeat(64), webhookSecret: "b".repeat(64), botToApiKey: "c".repeat(64) };
const entree = {
  adresseBot: "https://bot.example.org",
  adressePublique: "https://shameless.example.org/",
  guildId: "123456789012345678",
  secrets,
};

describe("construireRecapUchuu", () => {
  const recap = construireRecapUchuu(entree);

  it("liste côté Shameless les variables Compose, avec composant, rôle et valeur", () => {
    const shameless = recap.variables.filter((v) => v.composant === "Shameless");
    expect(shameless.map((v) => [v.nom, v.valeur])).toEqual([
      ["SHAMELESS_BOT_URL", "https://bot.example.org"],
      ["SHAMELESS_WEB_URL", "https://shameless.example.org"],
      ["SHAMELESS_BOT_API_KEY", secrets.botApiKey],
      ["SHAMELESS_WEBHOOK_SECRET", secrets.webhookSecret],
    ]);
    for (const v of recap.variables) expect(v.role.length).toBeGreaterThan(10);
  });

  it("liste côté Uchuu uniquement ce qui est confirmé, avec l'ID du serveur Discord repris", () => {
    const uchuu = recap.variables.filter((v) => v.composant === "Uchuu");
    expect(uchuu.map((v) => [v.nom, v.valeur])).toEqual([
      ["SHAMELESS_BOT_API_KEY", secrets.botApiKey],
      ["SHAMELESS_WEBHOOK_SECRET", secrets.webhookSecret],
      ["SHAMELESS_WEB_URL", "https://shameless.example.org"],
      ["SHAMELESS_GUILD_ID", "123456789012345678"],
    ]);
  });

  it("produit un bloc .env copiable pour Uchuu", () => {
    const lignes = recap.envUchuu.split("\n").filter((l) => l !== "" && !l.startsWith("#"));
    expect(lignes).toEqual([
      `SHAMELESS_BOT_API_KEY=${secrets.botApiKey}`,
      `SHAMELESS_WEBHOOK_SECRET=${secrets.webhookSecret}`,
      "SHAMELESS_WEB_URL=https://shameless.example.org",
      "SHAMELESS_GUILD_ID=123456789012345678",
    ]);
  });

  it("n'expose jamais SHAMELESS_BOT_TO_API_KEY comme à reporter dans Uchuu", () => {
    expect(recap.variables.some((v) => v.nom === "SHAMELESS_BOT_TO_API_KEY")).toBe(false);
    expect(recap.envUchuu).not.toContain("BOT_TO_API_KEY");
    expect(recap.envUchuu).not.toContain(secrets.botToApiKey);
  });

  it("garde la clé bot → API dans un champ séparé « à confirmer »", () => {
    expect(recap.aConfirmer).toMatchObject({
      nom: "SHAMELESS_BOT_TO_API_KEY",
      composant: "Shameless",
      valeur: secrets.botToApiKey,
    });
    expect(recap.aConfirmer.note).toMatch(/à confirmer/i);
  });

  it("refuse un ID de serveur Discord qui n'est pas un identifiant", () => {
    expect(() => construireRecapUchuu({ ...entree, guildId: "abc" })).toThrow(/serveur Discord/);
  });
});
