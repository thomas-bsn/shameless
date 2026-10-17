import { dateHeureLabel } from "@/lib/dates";

describe("dateHeureLabel", () => {
  it("écrit le jour, la date et l'heure d'une Game en français", () => {
    expect(dateHeureLabel(new Date(2026, 9, 16, 22, 40).toISOString())).toBe("ven. 16 oct., 22:40");
  });
});
