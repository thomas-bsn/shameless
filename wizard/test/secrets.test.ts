import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { genererSecretsUchuu } from "../src/secrets.ts";

describe("genererSecretsUchuu", () => {
  it("génère les trois secrets en hexadécimal, d'au moins 32 octets", () => {
    const s = genererSecretsUchuu();
    for (const v of [s.botApiKey, s.webhookSecret, s.botToApiKey]) {
      assert.match(v, /^[0-9a-f]{64,}$/);
    }
  });

  it("les trois secrets sont distincts, et différents d'un appel à l'autre", () => {
    const a = genererSecretsUchuu();
    const b = genererSecretsUchuu();
    assert.equal(new Set([a.botApiKey, a.webhookSecret, a.botToApiKey]).size, 3);
    assert.notEqual(a.botApiKey, b.botApiKey);
  });

  it("tire de nouveau si la source aléatoire produit deux fois la même valeur", () => {
    const sequence = [1, 1, 1, 2, 3].map((n) => new Uint8Array(32).fill(n));
    let i = 0;
    const s = genererSecretsUchuu((n) => {
      assert.equal(n, 32);
      return sequence[i++];
    });
    assert.equal(s.botApiKey, "01".repeat(32));
    assert.equal(s.webhookSecret, "02".repeat(32));
    assert.equal(s.botToApiKey, "03".repeat(32));
  });

  it("refuse une source qui rend moins d'octets que demandé", () => {
    assert.throws(() => genererSecretsUchuu(() => new Uint8Array(8)));
  });
});
