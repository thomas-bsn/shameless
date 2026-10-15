import { screen } from "@testing-library/react";
import { mockApi } from "@/test/api";
import { renderComponent } from "@/test/render";
import { ApiStatus } from "./api-status";

describe("ApiStatus", () => {
  it("shows the health reported by the API", async () => {
    const fetch = mockApi({ "GET /health": new Response("Healthy", { headers: { "Content-Type": "text/plain" } }) });

    renderComponent(<ApiStatus />);

    expect(await screen.findByText("Healthy")).toBeInTheDocument();
    expect(new URL(fetch.mock.calls[0]![0].url).pathname).toBe("/api/health");
  });

  it("shows an unhealthy API as such", async () => {
    mockApi({ "GET /health": new Response("Unhealthy", { status: 503 }) });

    renderComponent(<ApiStatus />);

    expect(await screen.findByText("Unhealthy")).toBeInTheDocument();
  });

  it("says the API is unreachable when nothing answers", async () => {
    mockApi({ "GET /health": () => Promise.reject(new TypeError("Failed to fetch")) });

    renderComponent(<ApiStatus />);

    expect(await screen.findByText("Injoignable")).toBeInTheDocument();
  });
});
