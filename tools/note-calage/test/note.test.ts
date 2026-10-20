import assert from "node:assert/strict";
import { describe, it } from "node:test";
import { noter, parametresParDefaut, roleDe } from "../src/note.ts";
import type { Game, Participant } from "../src/types.ts";
import { joueur, T0 } from "./donnees.ts";

/** Game de 30 minutes. L'équipe 100 totalise 24 kills ; le joueur noté (index 0) est milieu avec exactement les valeurs « Moyen » du mid. */
function gameDuMid(surcharge: Partial<Participant> = {}, role = "MIDDLE"): Game {
  const cible = joueur("Zed", role, 100, [6, 4, 6], {
    totalMinionsKilled: 200, neutralMinionsKilled: 10, // 210 CS / 30 min = 7
    totalDamageDealtToChampions: 21000, // 700 / min
    visionScore: 24, // 0.8 / min
    damageDealtToObjectives: 9000, // 300 / min
    goldEarned: 12000,
    ...surcharge,
  });
  const equipe = [6, 6, 6, 0].map((k, i) => joueur(`Allie${i}`, "TOP", 100, [k, 2, 2]));
  const adverses = Array.from({ length: 5 }, (_, i) => joueur(`Ennemi${i}`, "TOP", 200, [3, 4, 4]));
  return { matchId: "EUW1_1", debut: T0, duree: 1800, participants: [cible, ...equipe, ...adverses] };
}

describe("note d'un joueur dans une game", () => {
  it("vaut 50 quand toutes les valeurs sont à la référence Moyen du rôle", () => {
    const g = gameDuMid();
    const n = noter(g, g.participants[0]!, parametresParDefaut());
    assert.equal(n.role, "MID");
    assert.equal(Math.round(n.note * 100) / 100, 50);
  });

  it("pondère les critères selon le rôle (ADR 0011) : dégâts excellents au mid = 62,5", () => {
    const g = gameDuMid({ totalDamageDealtToChampions: 30000 }); // 1000/min = Excellent
    const n = noter(g, g.participants[0]!, parametresParDefaut());
    assert.equal(Math.round(n.note * 100) / 100, 62.5);
  });

  it("note le rôle de match-v5 : un support n'est pas noté sur le CS ni les objectifs", () => {
    const g = gameDuMid({ totalMinionsKilled: 0, neutralMinionsKilled: 0, damageDealtToObjectives: 0 }, "UTILITY");
    const n = noter(g, g.participants[0]!, parametresParDefaut());
    assert.equal(n.role, "SUPPORT");
    assert.ok(!n.composantes.some((c) => c.critere === "csParMinute" || c.critere === "objectifsParMinute"));
  });

  it("renormalise quand une stat manque", () => {
    const g = gameDuMid({ visionScore: undefined });
    const n = noter(g, g.participants[0]!, parametresParDefaut());
    assert.ok(!n.composantes.some((c) => c.critere === "visionParMinute"));
    assert.equal(Math.round(n.note * 100) / 100, 50);
  });

  it("lit le rôle dans teamPosition, sinon individualPosition, sinon inconnu", () => {
    assert.equal(roleDe({ championName: "A", teamPosition: "BOTTOM" }), "ADC");
    assert.equal(roleDe({ championName: "A", teamPosition: "", individualPosition: "JUNGLE" }), "JUNGLE");
    assert.equal(roleDe({ championName: "A", teamPosition: "Invalid", individualPosition: "Invalid" }), "INCONNU");
    assert.equal(roleDe({ championName: "A" }), "INCONNU");
  });

  it("n'utilise pas la victoire ni la part de la game tant que leur poids est nul", () => {
    const a = gameDuMid();
    const gagnant = noter(a, a.participants[0]!, parametresParDefaut()).note;
    a.participants[0]!.win = false;
    assert.equal(noter(a, a.participants[0]!, parametresParDefaut()).note, gagnant);
  });

  it("composante victoire : poids 100 sur 100 + 100 → moyenne du score de victoire (100 ou 0) et du reste", () => {
    const p = parametresParDefaut();
    p.roles.MID.poidsVictoire = 100; // autant que tous les autres critères réunis (100)
    const g = gameDuMid();
    g.participants[0]!.win = true;
    assert.equal(noter(g, g.participants[0]!, p).note, 75); // (50 * 100 + 100 * 100) / 200
    g.participants[0]!.win = false;
    assert.equal(noter(g, g.participants[0]!, p).note, 25);
  });

  it("composante relative à la game : part des kills de l'équipe (6 sur 24 = 25 %)", () => {
    const p = parametresParDefaut();
    p.roles.MID.criteres.partDesKills = { poids: 100, faible: 0.05, moyen: 0.2, excellent: 0.35 };
    const g = gameDuMid();
    // 0,25 est à un tiers de Moyen → Excellent : score 50 + 50 / 3 ; poids égaux avec le reste (note 50)
    const attendu = (50 + (50 + 50 / 3)) / 2;
    assert.ok(Math.abs(noter(g, g.participants[0]!, p).note - attendu) < 1e-9);
  });

  it("composante or par minute : 12000 or en 30 min = 400/min", () => {
    const p = parametresParDefaut();
    p.roles.MID.criteres.orParMinute = { poids: 100, faible: 200, moyen: 300, excellent: 500 };
    const g = gameDuMid();
    const attendu = (50 + 75) / 2; // 400 : 50 + 50 * 100/200
    assert.ok(Math.abs(noter(g, g.participants[0]!, p).note - attendu) < 1e-9);
  });
});
