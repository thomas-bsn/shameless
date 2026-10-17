// PROTOTYPE (issue #68) — jetable. État en mémoire, aucune API : on juge l'apparence et le rythme, pas le backend.
import { useCallback, useEffect, useMemo, useRef, useState } from "react";

export const STEPS = [
  { key: "intro", label: "Introduction" },
  { key: "discord", label: "Discord" },
  { key: "options", label: "Options" },
  { key: "recap", label: "Récapitulatif" },
  { key: "fin", label: "Installation" },
] as const;
export type StepKey = (typeof STEPS)[number]["key"];

export interface Values {
  url: string;
  clientId: string;
  clientSecret: string;
  guildId: string;
  adminId: string;
  riot: boolean;
  riotKey: string;
  uchuu: boolean;
  timezone: string;
}

export const EMPTY: Values = {
  url: "",
  clientId: "",
  clientSecret: "",
  guildId: "",
  adminId: "",
  riot: false,
  riotKey: "",
  uchuu: false,
  timezone: "Europe/Paris",
};

export const EXAMPLE: Values = {
  url: "https://shameless.example.org",
  clientId: "1234567890123456789",
  clientSecret: "dGhpcy1pcy1ub3QtYS1yZWFsLXNlY3JldA",
  guildId: "9876543210987654321",
  adminId: "1122334455667788990",
  riot: false,
  riotKey: "",
  uchuu: true,
  timezone: "Europe/Paris",
};

export const TIMEZONES = ["Europe/Paris", "Europe/London", "America/New_York", "America/Montreal", "Asia/Tokyo", "UTC"];

const snowflake = /^\d{17,20}$/;

export type Errors = Partial<Record<keyof Values, string>>;

export function validate(v: Values, step: StepKey): Errors {
  const e: Errors = {};
  if (step === "discord" || step === "recap") {
    if (!/^https?:\/\/[^\s/]+\.?[^\s]*$/.test(v.url)) e.url = "Adresse invalide : commence par https:// (ex. https://shameless.example.org).";
    else if (v.url.endsWith("/")) e.url = "Retire le « / » final.";
    if (!snowflake.test(v.clientId)) e.clientId = "Le Client ID est un nombre de 17 à 20 chiffres.";
    if (v.clientSecret.length < 20) e.clientSecret = "Le Client Secret semble trop court (≥ 20 caractères).";
    if (!snowflake.test(v.guildId)) e.guildId = "L'identifiant du serveur est un nombre de 17 à 20 chiffres.";
    if (!snowflake.test(v.adminId)) e.adminId = "Il faut au moins un administrateur (identifiant Discord, 17 à 20 chiffres).";
  }
  if (step === "options" || step === "recap") {
    if (v.riot && !/^RGAPI-[\w-]{20,}$/.test(v.riotKey)) e.riotKey = "Une clé Riot commence par « RGAPI- ».";
  }
  return e;
}

export const callbackUrl = (v: Values) => `${v.url || "https://…"}/api/auth/discord/callback`;
export const mask = (s: string) => (s ? "•".repeat(Math.min(s.length, 24)) : "—");

/** Champs de chaque étape pour savoir quelle étape contient une erreur. */
export const FIELDS_BY_STEP: Record<StepKey, (keyof Values)[]> = {
  intro: [],
  discord: ["url", "clientId", "clientSecret", "guildId", "adminId"],
  options: ["riot", "riotKey", "uchuu", "timezone"],
  recap: [],
  fin: [],
};

export type InstallPhase = "idle" | "running" | "ok" | "error";
export const INSTALL_STEPS = [
  "Écriture de la configuration",
  "Démarrage de la base de données",
  "Migrations",
  "Démarrage de l'API",
  "Vérification de la connexion Discord",
];

export function useWizard() {
  const [step, setStep] = useState<StepKey>("intro");
  const [values, setValues] = useState<Values>(EMPTY);
  const [touched, setTouched] = useState<Set<string>>(new Set());
  const [attempted, setAttempted] = useState<Set<StepKey>>(new Set());
  const [simulateFailure, setSimulateFailure] = useState(false);
  const [phase, setPhase] = useState<InstallPhase>("idle");
  const [progress, setProgress] = useState(0);
  const timer = useRef<ReturnType<typeof setInterval>>(undefined);

  const set = useCallback(<K extends keyof Values>(k: K, val: Values[K]) => setValues((p) => ({ ...p, [k]: val })), []);
  const touch = useCallback((k: string) => setTouched((p) => new Set(p).add(k)), []);

  const errors = useMemo(() => validate(values, "recap"), [values]);
  /** Erreur affichée : seulement après avoir touché le champ ou tenté de continuer. */
  const shown = (k: keyof Values, forStep: StepKey): string | undefined =>
    touched.has(k) || attempted.has(forStep) ? errors[k] : undefined;

  const stepIndex = STEPS.findIndex((s) => s.key === step);
  const stepValid = (s: StepKey) => FIELDS_BY_STEP[s].every((f) => !errors[f]);

  const go = (s: StepKey) => setStep(s);
  const next = () => {
    if (!stepValid(step)) {
      setAttempted((p) => new Set(p).add(step));
      return false;
    }
    setStep(STEPS[Math.min(stepIndex + 1, STEPS.length - 1)].key);
    return true;
  };
  const back = () => setStep(STEPS[Math.max(stepIndex - 1, 0)].key);

  const install = useCallback(() => {
    setStep("fin");
    setPhase("running");
    setProgress(0);
  }, []);

  useEffect(() => {
    if (phase !== "running") return;
    timer.current = setInterval(() => {
      setProgress((p) => {
        const n = p + 1;
        if (simulateFailure && n === 4) {
          clearInterval(timer.current);
          setPhase("error");
          return 3;
        }
        if (n >= INSTALL_STEPS.length) {
          clearInterval(timer.current);
          setPhase("ok");
          return INSTALL_STEPS.length;
        }
        return n;
      });
    }, 900);
    return () => clearInterval(timer.current);
  }, [phase, simulateFailure]);

  return {
    step, stepIndex, go, next, back, values, set, touch, shown, errors, stepValid,
    fill: () => setValues(EXAMPLE),
    reset: () => { setValues(EMPTY); setTouched(new Set()); setAttempted(new Set()); setStep("intro"); setPhase("idle"); },
    simulateFailure, setSimulateFailure, phase, progress, install,
    retry: () => { setPhase("running"); setProgress(0); setSimulateFailure(false); },
  };
}
export type Wizard = ReturnType<typeof useWizard>;
