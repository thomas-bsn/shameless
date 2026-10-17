import { genererSecretsUchuu } from "@/lib/wizard/secrets";

describe("genererSecretsUchuu", () => {
  it("génère les trois secrets en hexadécimal, d'au moins 32 octets", () => {
    const s = genererSecretsUchuu();
    for (const v of [s.botApiKey, s.webhookSecret, s.botToApiKey]) {
      expect(v).toMatch(/^[0-9a-f]{64,}$/);
    }
  });

  it("les trois secrets sont distincts, et différents d'un appel à l'autre", () => {
    const a = genererSecretsUchuu();
    const b = genererSecretsUchuu();
    expect(new Set([a.botApiKey, a.webhookSecret, a.botToApiKey]).size).toBe(3);
    expect(a.botApiKey).not.toBe(b.botApiKey);
  });

  it("tire de nouveau si la source aléatoire produit deux fois la même valeur", () => {
    const sequence = [1, 1, 1, 2, 3].map((n) => new Uint8Array(32).fill(n));
    let i = 0;
    const s = genererSecretsUchuu((n) => {
      expect(n).toBe(32);
      return sequence[i++];
    });
    expect(s.botApiKey).toBe("01".repeat(32));
    expect(s.webhookSecret).toBe("02".repeat(32));
    expect(s.botToApiKey).toBe("03".repeat(32));
  });

  it("refuse une source qui rend moins d'octets que demandé", () => {
    expect(() => genererSecretsUchuu(() => new Uint8Array(8))).toThrow();
  });
});
