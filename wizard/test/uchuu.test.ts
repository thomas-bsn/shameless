import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { construireRecapUchuu, VARIABLES_BOT_A_SAISIR, NOMS_VARIABLES_UCHUU } from "../src/uchuu.ts";

const secrets = { botApiKey: "a".repeat(64), webhookSecret: "b".repeat(64), botToApiKey: "c".repeat(64) };
const entree = {
  adresseBot: "https://bot.example.org",
  adressePublique: "https://shameless.example.org/",
  guildId: "123456789012345678",
  secrets,
};

const paires = (r: ReturnType<typeof construireRecapUchuu>, composant: string) =>
  r.variables.filter((v) => v.composant === composant).map((v) => [v.nom, v.valeur]);

describe("construireRecapUchuu", () => {
  const recap = construireRecapUchuu(entree);

  it("liste côté Shameless les cinq variables écrites dans instance.env, avec composant, rôle et valeur", () => {
    assert.deepEqual(paires(recap, "Shameless"), [
      ["SHAMELESS_BOT_URL", "https://bot.example.org"],
      ["SHAMELESS_WEB_URL", "https://shameless.example.org"],
      ["SHAMELESS_BOT_API_KEY", secrets.botApiKey],
      ["SHAMELESS_WEBHOOK_SECRET", secrets.webhookSecret],
      ["SHAMELESS_BOT_TO_API_KEY", secrets.botToApiKey],
    ]);
    for (const v of recap.variables) assert.ok(v.role.length > 10, v.nom);
  });

  it("liste côté bot les secrets partagés, les adresses et l'ID du serveur Discord", () => {
    assert.deepEqual(paires(recap, "Uchuu"), [
      ["SHAMELESS_BOT_API_KEY", secrets.botApiKey],
      ["SHAMELESS_WEBHOOK_SECRET", secrets.webhookSecret],
      ["SHAMELESS_BOT_TO_API_KEY", secrets.botToApiKey],
      ["SHAMELESS_WEB_URL", "https://shameless.example.org"],
      ["SHAMELESS_GUILD_ID", "123456789012345678"],
      ["SHAMELESS_API_URL", "https://shameless.example.org/api"],
    ]);
  });

  it("produit un bloc .env copiable pour le bot, sans les variables de Shameless seul", () => {
    const lignes = recap.envUchuu.split("\n").filter((l) => l !== "" && !l.startsWith("#"));
    assert.deepEqual(
      lignes,
      paires(recap, "Uchuu").map(([nom, valeur]) => `${nom}=${valeur}`),
    );
    assert.ok(!recap.envUchuu.includes("SHAMELESS_BOT_URL"));
  });

  it("signale SHAMELESS_API_URL comme à confirmer, dans son rôle et dans le bloc .env", () => {
    const api = recap.variables.find((v) => v.nom === "SHAMELESS_API_URL")!;
    assert.match(api.role, /À confirmer/);
    assert.match(recap.envUchuu, /# SHAMELESS_API_URL : à confirmer/);
    for (const v of recap.variables.filter((v) => v.nom !== "SHAMELESS_API_URL")) assert.doesNotMatch(v.role, /À confirmer/);
  });

  it("les valeurs partagées sont identiques des deux côtés", () => {
    for (const nom of ["SHAMELESS_BOT_API_KEY", "SHAMELESS_WEBHOOK_SECRET", "SHAMELESS_BOT_TO_API_KEY", "SHAMELESS_WEB_URL"]) {
      const valeurs = recap.variables.filter((v) => v.nom === nom).map((v) => v.valeur);
      assert.equal(valeurs.length, 2, nom);
      assert.equal(valeurs[0], valeurs[1], nom);
    }
  });

  it("liste les variables du bot à saisir à la main, facultatives ou non, et renvoie à la doc du bot", () => {
    assert.deepEqual(
      VARIABLES_BOT_A_SAISIR.map((v) => [v.nom, v.facultatif]),
      [
        ["SHAMELESS_TEAM1_VOICE_ID", false],
        ["SHAMELESS_TEAM2_VOICE_ID", false],
        ["SHAMELESS_ANNOUNCE_CHANNEL_ID", false],
        ["SHAMELESS_VOTE_CHANNEL_ID", false],
        ["SHAMELESS_PRONOSTICS_CHANNEL_ID", true],
        ["SHAMELESS_CLIPS_CHANNEL_ID", true],
        ["SHAMELESS_CLIPS_MAX_BYTES", true],
      ],
    );
    assert.match(recap.docBot, /docs\/shameless\.md/);
    assert.match(recap.docBot, /harceleur_bot/);
  });

  it("propose une commande curl Bearer sur voice-candidates, sans que le wizard n'appelle le bot", () => {
    assert.match(recap.verification, /^curl .*Authorization: Bearer a{64}.* https:\/\/bot\.example\.org\/internal\/shameless\/voice-candidates$/);
  });

  it("fige les noms de variables du contrat Shameless ↔ Uchuu (ADR 0016)", () => {
    assert.deepEqual(NOMS_VARIABLES_UCHUU, {
      shameless: ["SHAMELESS_BOT_URL", "SHAMELESS_WEB_URL", "SHAMELESS_BOT_API_KEY", "SHAMELESS_WEBHOOK_SECRET", "SHAMELESS_BOT_TO_API_KEY"],
      bot: ["SHAMELESS_BOT_API_KEY", "SHAMELESS_WEBHOOK_SECRET", "SHAMELESS_BOT_TO_API_KEY", "SHAMELESS_WEB_URL", "SHAMELESS_GUILD_ID", "SHAMELESS_API_URL"],
    });
  });

  it("refuse un ID de serveur Discord qui n'est pas un identifiant", () => {
    assert.throws(() => construireRecapUchuu({ ...entree, guildId: "abc" }), /serveur Discord/);
  });
});
