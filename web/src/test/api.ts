/** Réponse simulée : un corps JSON (200), ou une Response construite par le test. */
type Route = Response | object | ((request: Request) => Response | Promise<Response>);

export const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

export const problem = (status: number, detail?: string, title?: string) =>
  new Response(JSON.stringify({ status, detail, title }), {
    status,
    headers: { "Content-Type": "application/problem+json" },
  });

/**
 * Remplace fetch par une API simulée. Les clés sont « MÉTHODE /chemin » (sans /api ni query).
 * Une route absente répond 404. Renvoie le mock pour inspecter les appels.
 */
export function mockApi(routes: Record<string, Route>) {
  const fetchMock = vi.fn(async (input: Request) => {
    const url = new URL(input.url);
    const key = `${input.method} ${url.pathname.replace(/^\/api/, "")}`;
    const route = routes[key];
    if (route === undefined) return problem(404);
    if (typeof route === "function") return route(input);
    if (route instanceof Response) return route.clone();
    return json(route);
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}
