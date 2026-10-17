import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { FUSEAU_PAR_DEFAUT, validerAdresseBot, validerCleRiot, validerFuseau } from "../src/validation.ts";

describe("validerAdresseBot", () => {
  it("accepte une adresse https d'hôte, avec port éventuel", () => {
    assert.deepEqual(validerAdresseBot("https://bot.example.org"), { ok: true, valeur: "https://bot.example.org", avertissements: [] });
    const r = validerAdresseBot("  https://bot.example.org:9443 ");
    assert.ok(r.ok && r.valeur === "https://bot.example.org:9443");
  });

  it("refuse une adresse vide avec le message de l'étape", () => {
    assert.deepEqual(validerAdresseBot("   "), { ok: false, erreur: "Indique l'adresse de ton bot, ou désactive l'intégration." });
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
      assert.equal(r.ok, false);
      return r.ok ? "" : r.erreur;
    });
    assert.equal(new Set(erreurs).size, erreurs.length);
    assert.match(erreurs[1], /https/);
    assert.match(erreurs[2], /« \/ » final/);
    assert.match(erreurs[5], /identifiants/);
    assert.equal(validerAdresseBot("https://bot.example.org#haut").ok, false);
    assert.equal(validerAdresseBot("ftp://bot.example.org").ok, false);
  });

  it("n'écrit jamais les identifiants saisis dans le message", () => {
    const r = validerAdresseBot("https://user:supersecret@bot.example.org");
    assert.ok(!JSON.stringify(r).includes("supersecret"));
  });

  it("avertit sans bloquer pour localhost, une IP privée ou un nom de service Docker", () => {
    for (const a of ["https://localhost", "https://127.0.0.1:9000", "https://10.0.0.5", "https://172.20.1.1", "https://192.168.1.10", "https://bot", "https://bot.local"]) {
      const r = validerAdresseBot(a);
      assert.ok(r.ok, a);
      assert.equal(r.avertissements.length, 1);
      assert.match(r.avertissements[0], /injoignable depuis le conteneur de l'API/);
    }
  });

  it("tolère http seulement pour localhost ou le réseau privé", () => {
    const local = validerAdresseBot("http://localhost:9000");
    assert.ok(local.ok && local.valeur === "http://localhost:9000");
    assert.equal(validerAdresseBot("http://192.168.1.10:9000").ok, true);
    assert.equal(validerAdresseBot("http://172.32.0.1").ok, false);
  });
});

describe("validerCleRiot", () => {
  it("accepte une clé vide : l'Instance fonctionne sans les fonctions Riot", () => {
    assert.deepEqual(validerCleRiot(""), { ok: true, valeur: "" });
    assert.deepEqual(validerCleRiot("  "), { ok: true, valeur: "" });
  });

  it("accepte une clé RGAPI-…", () => {
    const cle = "RGAPI-12345678-abcd-4ef0-9abc-0123456789ab";
    assert.deepEqual(validerCleRiot(` ${cle} `), { ok: true, valeur: cle });
  });

  it("refuse une clé sans préfixe ou mal formée, sans la répéter", () => {
    for (const cle of ["12345678-abcd", "RGAPI-", "RGAPI-abc def", "rgapi-secret-secret"]) {
      const r = validerCleRiot(cle);
      assert.equal(r.ok, false);
      assert.ok(!JSON.stringify(r).includes(cle));
    }
    const r = validerCleRiot("pas-une-cle");
    assert.ok(!r.ok && /RGAPI-/.test(r.erreur));
  });
});

describe("validerFuseau", () => {
  it("propose Europe/Paris par défaut", () => {
    assert.equal(FUSEAU_PAR_DEFAUT, "Europe/Paris");
    assert.deepEqual(validerFuseau(""), { ok: true, valeur: "Europe/Paris" });
  });

  it("accepte un fuseau IANA et le rend dans sa casse canonique", () => {
    assert.deepEqual(validerFuseau("America/New_York"), { ok: true, valeur: "America/New_York" });
    assert.deepEqual(validerFuseau("europe/paris"), { ok: true, valeur: "Europe/Paris" });
  });

  it("refuse un fuseau inconnu", () => {
    assert.equal(validerFuseau("Paris").ok, false);
    assert.equal(validerFuseau("UTC+2").ok, false);
  });
});
