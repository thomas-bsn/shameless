import createFetchClient from "openapi-fetch";
import createClient from "openapi-react-query";
import type { components, paths } from "./schema";

/**
 * Client généré depuis contracts/openapi.json (`npm run gen:api`). L'API est servie sous
 * /api sur la même origine que le front : le cookie de session part avec chaque appel.
 */
export const fetchClient = createFetchClient<paths>({
  // URL absolue : Request l'exige hors navigateur (tests).
  baseUrl: `${window.location.origin}/api`,
  credentials: "same-origin",
  // Résolu à chaque appel, pour que les tests puissent remplacer fetch.
  fetch: (request) => globalThis.fetch(request),
});

export const $api = createClient(fetchClient);

export type Schemas = components["schemas"];
