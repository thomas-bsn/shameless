import { loginUrl } from "./session";

describe("loginUrl", () => {
  it("starts the Discord login of the API and comes back to the given page", () => {
    expect(loginUrl("/joueurs/3?onglet=stats")).toBe(
      "/api/auth/discord/login?returnUrl=%2Fjoueurs%2F3%3Fonglet%3Dstats",
    );
  });

  it("only accepts local paths as the return page", () => {
    expect(loginUrl("https://evil.example/")).toBe("/api/auth/discord/login?returnUrl=%2F");
    expect(loginUrl("//evil.example")).toBe("/api/auth/discord/login?returnUrl=%2F");
  });
});
