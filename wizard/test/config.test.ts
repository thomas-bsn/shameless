import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { buildInstanceEnv, serializeEnv, validateConfig } from "../src/config.ts";

const SUPERADMIN = "111111111111111111";
const valide = {
  publicUrl: "https://shameless.example.org",
  clientId: "123456789012345678",
  clientSecret: "abcdefghijklmnopqrstuvwxyz012345",
  guildId: "987654321098765432",
};

describe("validation de la configuration", () => {
  it("accepte une configuration complète", () => {
    const r = validateConfig(valide);
    assert.deepEqual(r.errors, {});
    assert.ok(r.value);
  });

  it("exige tous les champs et les nomme", () => {
    const r = validateConfig({});
    assert.deepEqual(Object.keys(r.errors).sort(), ["clientId", "clientSecret", "guildId", "publicUrl"]);
    assert.equal(r.value, undefined);
  });

  it("exige HTTPS pour une adresse publique", () => {
    const r = validateConfig({ ...valide, publicUrl: "http://shameless.example.org" });
    assert.match(r.errors.publicUrl ?? "", /https/);
  });

  it("tolère HTTP pour un accès local", () => {
    for (const url of ["http://localhost:8080", "http://127.0.0.1:8080"]) {
      assert.deepEqual(validateConfig({ ...valide, publicUrl: url }).errors, {}, url);
    }
  });

  it("refuse une adresse avec un chemin, une requête ou des identifiants", () => {
    for (const url of ["https://a.example/app", "https://a.example/?x=1", "https://u:p@a.example", "pas une url"]) {
      assert.ok(validateConfig({ ...valide, publicUrl: url }).errors.publicUrl, url);
    }
  });

  it("accepte un « / » final sur l'adresse et le retire", () => {
    const r = validateConfig({ ...valide, publicUrl: "https://shameless.example.org/" });
    assert.equal(r.value?.publicUrl, "https://shameless.example.org");
  });

  it("vérifie le format des identifiants Discord", () => {
    const r = validateConfig({ ...valide, clientId: "abc", guildId: "12" });
    assert.ok(r.errors.clientId && r.errors.guildId);
    for (const m of [r.errors.clientId, r.errors.guildId]) assert.match(m, /un nombre de 17 à 20 chiffres/);
  });

  it("ne demande plus d'identifiant d'administrateur : il vient de la connexion Discord", () => {
    const r = validateConfig({ ...valide, adminIds: "x" });
    assert.deepEqual(r.errors, {});
    assert.equal("adminIds" in (r.value ?? {}), false);
  });

  it("refuse un secret contenant un espace ou un saut de ligne", () => {
    assert.ok(validateConfig({ ...valide, clientSecret: "abc def abc def abc def abc def" }).errors.clientSecret);
    assert.ok(validateConfig({ ...valide, clientSecret: "abcdefghijklmnopqrstuvwxyz\nx" }).errors.clientSecret);
  });
});

describe("variables d'Instance calculées", () => {
  const value = validateConfig(valide).value!;
  const env = buildInstanceEnv({ ...value, superadminId: SUPERADMIN }, { postgresPassword: "pg-secret" });

  it("calcule le callback Discord et les origines depuis l'adresse publique", () => {
    assert.equal(env.Auth__Discord__RedirectUri, "https://shameless.example.org/api/auth/discord/callback");
    assert.equal(env.Auth__AllowedReturnOrigins, "https://shameless.example.org");
  });

  it("transmet les valeurs Discord et le premier Superadministrateur (importé par l'API au premier démarrage, ADR 0017)", () => {
    assert.equal(env.Auth__Discord__ClientId, valide.clientId);
    assert.equal(env.Auth__Discord__ClientSecret, valide.clientSecret);
    assert.equal(env.Auth__Discord__AllowedGuildId, valide.guildId);
    assert.equal(env.Auth__AdminDiscordIds, SUPERADMIN);
  });

  it("injecte le mot de passe généré dans la connexion à la base", () => {
    assert.equal(env.POSTGRES_PASSWORD, "pg-secret");
    assert.match(env.ConnectionStrings__Shameless, /Host=postgres;.*Password=pg-secret/);
  });

  it("sert un domaine en HTTPS automatique et le local en HTTP sur le port 80", () => {
    assert.equal(env.SHAMELESS_SITE_ADDRESS, "shameless.example.org");
    const local = validateConfig({ ...valide, publicUrl: "http://localhost:8080" }).value!;
    assert.equal(buildInstanceEnv({ ...local, superadminId: SUPERADMIN }, { postgresPassword: "x" }).SHAMELESS_SITE_ADDRESS, ":80");
  });

  it("sert un domaine en HTTP sur le port 80 quand un proxy en amont termine le HTTPS", () => {
    assert.equal(buildInstanceEnv({ ...(validateConfig(valide).value!), superadminId: SUPERADMIN, tlsEnAmont: true }, { postgresPassword: "x" }).SHAMELESS_SITE_ADDRESS, ":80");
  });

  it("refuse d'écrire la configuration sans Superadministrateur connecté", () => {
    assert.throws(() => buildInstanceEnv(value, { postgresPassword: "x" }), /Superadministrateur/);
  });

  it("sérialise en fichier sourçable par sh, même avec une apostrophe", () => {
    const texte = serializeEnv({ A: "simple", B: "l'apo" });
    assert.equal(texte, "A='simple'\nB='l'\\''apo'\n");
  });
});
