import type { Schemas } from "@/api/client";

const byStatus: Record<number, string> = {
  400: "La demande est invalide.",
  401: "Ta session a expiré, reconnecte-toi.",
  403: "Tu n'as pas les droits pour faire ça.",
  404: "Introuvable.",
  409: "Conflit avec l'état actuel, recharge la page.",
};

const generic = "Une erreur est survenue, réessaie plus tard.";

function isProblem(error: unknown): error is Schemas["ProblemDetails"] {
  return typeof error === "object" && error !== null && "status" in error;
}

/**
 * Message lisible pour une erreur de l'API. Le detail d'un ProblemDetails est rédigé
 * pour l'utilisateur ; le title, lui, est le libellé anglais par défaut d'ASP.NET.
 */
export function problemMessage(error: unknown): string {
  if (!isProblem(error)) return generic;
  if (error.detail) return error.detail;
  return (error.status != null && byStatus[error.status]) || generic;
}

/** Statut HTTP d'une erreur de l'API, s'il est connu. */
export function problemStatus(error: unknown): number | undefined {
  return isProblem(error) ? (error.status ?? undefined) : undefined;
}
