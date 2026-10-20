import { dateHeureLabel, ilYALabel } from "@/lib/dates";

describe("dateHeureLabel", () => {
  it("écrit le jour, la date et l'heure d'une Game en français", () => {
    expect(dateHeureLabel(new Date(2026, 9, 16, 22, 40).toISOString())).toBe("ven. 16 oct., 22:40");
  });
});

describe("ilYALabel", () => {
  const maintenant = Date.parse("2026-10-20T12:00:00Z");
  it("garde une seule unité : minutes, heures puis jours", () => {
    expect(ilYALabel("2026-10-20T11:48:00Z", maintenant)).toBe("il y a 12 min");
    expect(ilYALabel("2026-10-20T09:00:00Z", maintenant)).toBe("il y a 3 h");
    expect(ilYALabel("2026-10-05T12:00:00Z", maintenant)).toBe("il y a 15 j");
  });
});
