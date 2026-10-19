import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { buildInstanceEnv, validateConfig } from "../src/config.ts";

const valide = {
  publicUrl: "https://shameless.example.org",
  clientId: "123456789012345678",
  clientSecret: "abcdefghijklmnopqrstuvwxyz012345",
  guildId: "987654321098765432",
};
const secrets = { botApiKey: "a".repeat(64), webhookSecret: "b".repeat(64), botToApiKey: "c".repeat(64) };
const gen = { postgresPassword: "pg-secret", uchuu: secrets };

describe("options facultatives (#71)", () => {
  it("laisse toutes les options vides : Riot absente, fuseau Europe/Paris, Uchuu ignoré", () => {
    const r = validateConfig(valide);
    assert.deepEqual(r.errors, {});
    assert.equal(r.value?.riotKey, "");
    assert.equal(r.value?.timezone, "Europe/Paris");
    assert.equal(r.value?.uchuu, false);
    const env = buildInstanceEnv({ ...r.value!, superadminId: "111111111111111111" }, { postgresPassword: "x" });
    assert.equal(env.RIOT_API_KEY, "");
    assert.equal(env.SHAMELESS_TIMEZONE, "Europe/Paris");
    for (const nom of Object.keys(env)) assert.ok(!/^SHAMELESS_(BOT|WEB|WEBHOOK)/.test(nom), nom);
  });

  it("valide la clé Riot et le fuseau avec les messages du module de validation, sans répéter la clé", () => {
    const r = validateConfig({ ...valide, riotKey: "secret-pas-riot", timezone: "Mars/Olympus" });
    assert.match(r.errors.riotKey ?? "", /RGAPI-/);
    assert.ok(!r.errors.riotKey?.includes("secret-pas-riot"));
    assert.ok(r.errors.timezone);
    const ok = validateConfig({ ...valide, riotKey: " RGAPI-abc-123 ", timezone: "europe/paris" });
    assert.equal(ok.value?.riotKey, "RGAPI-abc-123");
    assert.equal(ok.value?.timezone, "Europe/Paris");
  });

  it("exige une adresse de bot valide quand Uchuu est activé, et seulement alors", () => {
    assert.ok(validateConfig({ ...valide, uchuu: true, botUrl: "" }).errors.botUrl);
    assert.ok(validateConfig({ ...valide, uchuu: true, botUrl: "http://bot.example.org" }).errors.botUrl);
    assert.deepEqual(validateConfig({ ...valide, uchuu: false, botUrl: "n'importe quoi" }).errors, {});
  });

  it("avertit sans bloquer quand l'adresse du bot est locale", () => {
    const r = validateConfig({ ...valide, uchuu: true, botUrl: "http://localhost:9000" });
    assert.deepEqual(r.errors, {});
    assert.match(r.warnings[0], /injoignable depuis le conteneur de l'API/);
  });

  it("écrit la clé Riot et le fuseau dans instance.env", () => {
    const v = validateConfig({ ...valide, riotKey: "RGAPI-abc-123", timezone: "America/New_York" }).value!;
    const env = buildInstanceEnv({ ...v, superadminId: "111111111111111111" }, gen);
    assert.equal(env.RIOT_API_KEY, "RGAPI-abc-123");
    assert.equal(env.SHAMELESS_TIMEZONE, "America/New_York");
  });

  it("écrit les variables Uchuu avec les trois secrets distincts quand l'intégration est activée", () => {
    const v = validateConfig({ ...valide, uchuu: true, botUrl: "https://bot.example.org" }).value!;
    const env = buildInstanceEnv({ ...v, superadminId: "111111111111111111" }, gen);
    assert.equal(env.SHAMELESS_BOT_URL, "https://bot.example.org");
    assert.equal(env.SHAMELESS_WEB_URL, "https://shameless.example.org");
    assert.equal(env.SHAMELESS_BOT_API_KEY, secrets.botApiKey);
    assert.equal(env.SHAMELESS_WEBHOOK_SECRET, secrets.webhookSecret);
    assert.equal(env.SHAMELESS_BOT_TO_API_KEY, secrets.botToApiKey);
  });

  it("refuse d'écrire Uchuu sans secrets générés", () => {
    const v = validateConfig({ ...valide, uchuu: true, botUrl: "https://bot.example.org" }).value!;
    assert.throws(() => buildInstanceEnv({ ...v, superadminId: "111111111111111111" }, { postgresPassword: "x" }), /secrets Uchuu/);
  });
});
