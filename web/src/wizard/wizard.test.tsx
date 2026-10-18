import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { WizardApp } from "./wizard-app";

type Json = Record<string, unknown>;

/** Faux serveur du wizard : le front est testé à sa frontière HTTP, comme le fait l'Opérateur. */
function fauxServeur(initial: Json = { phase: "locked" }) {
  const s = {
    etat: initial,
    codeAttendu: "ABCD-EFGH-JKMN-PQRS",
    installations: [] as Json[],
    reponseInstall: null as null | { status: number; json: Json },
    /** Récapitulatif Uchuu que le serveur renvoie à l'installation (construit côté serveur, testé là-bas). */
    recap: null as null | Json,
    /** Compte Discord connecté par l'Opérateur (renvoyé par l'état une fois le callback traité côté serveur). */
    discord: null as null | { id: string; username: string },
    discordError: "",
    demarragesDiscord: [] as Json[],
    /** Fenêtre ouverte vers Discord (stub de window.open). */
    fenetre: { location: { href: "" }, close: vi.fn() },
    // Un champ est invalide s'il est vide ou commence par « bad ».
    // Obligatoires : les champs Discord et l'adresse ; la clé Riot est facultative ; le bot n'est exigé que si Uchuu est activé.
    erreurs: (c: Record<string, unknown>) => {
      const invalides = ["publicUrl", "clientId", "clientSecret", "guildId"].filter((k) => !c[k] || String(c[k]).startsWith("bad"));
      if (String(c.riotKey ?? "").startsWith("bad")) invalides.push("riotKey");
      if (c.uchuu === true && (!c.botUrl || String(c.botUrl).startsWith("bad"))) invalides.push("botUrl");
      return Object.fromEntries(invalides.map((k) => [k, `Erreur serveur sur ${k}.`]));
    },
  };
  const repondre = (status: number, json: Json) => Promise.resolve(new Response(JSON.stringify(json), { status, headers: { "content-type": "application/json" } }));
  vi.stubGlobal(
    "fetch",
    vi.fn((url: string, init?: RequestInit) => {
      const route = url.replace("/_setup/api/", "");
      const corps = init?.body ? (JSON.parse(init.body as string) as Record<string, string>) : {};
      const avertissements = String(corps.botUrl ?? "").startsWith("http://localhost") ? ["Adresse locale : injoignable depuis le conteneur de l'API."] : [];
      if (route === "state")
        return repondre(200, { ...s.etat, ...(s.discord ? { discord: s.discord } : {}), ...(s.discordError ? { discordError: s.discordError } : {}) });
      if (route === "discord/start") {
        s.demarragesDiscord.push(corps);
        return repondre(200, { authorizeUrl: "https://discord.com/oauth2/authorize?state=abc", redirectUri: `${corps.publicUrl}/_setup/api/discord/callback` });
      }
      if (route === "unlock") {
        if (corps.code !== s.codeAttendu) return repondre(401, { message: "Code incorrect." });
        s.etat = { phase: "configuring" };
        return repondre(200, s.etat);
      }
      if (route === "validate")
        return repondre(200, { errors: s.erreurs(corps), warnings: avertissements, callbackUrl: corps.publicUrl ? `${corps.publicUrl}/api/auth/discord/callback` : null, wizardRedirectUri: corps.publicUrl ? `${corps.publicUrl}/_setup/api/discord/callback` : null });
      if (route === "install") {
        s.installations.push(corps);
        if (s.reponseInstall) return repondre(s.reponseInstall.status, s.reponseInstall.json);
        s.etat = { phase: "installing", publicUrl: corps.publicUrl, ...(s.recap ? { uchuu: s.recap } : {}) };
        return repondre(202, { phase: "installing", warnings: [], ...(s.recap ? { uchuu: s.recap } : {}) });
      }
      return repondre(404, {});
    }),
  );
  vi.stubGlobal("open", vi.fn(() => s.fenetre));
  return s;
}

const saisies = {
  "Adresse publique": "https://shameless.example.org",
  "Client ID": "123456789012345678",
  "Client Secret": "SECRET-DISCORD-abcdefghijklmnop",
  "Identifiant du serveur Discord": "987654321098765432",
};

async function remplir(user: ReturnType<typeof userEvent.setup>, champs: Partial<typeof saisies> = saisies) {
  for (const [label, valeur] of Object.entries(champs)) await user.type(screen.getByLabelText(label), valeur);
}

async function deverrouillerEtCommencer(user: ReturnType<typeof userEvent.setup>) {
  await user.type(await screen.findByLabelText("Code d'accès"), "ABCD-EFGH-JKMN-PQRS");
  await user.click(screen.getByRole("button", { name: "Déverrouiller" }));
  await user.click(await screen.findByRole("button", { name: "Commencer" }));
}

/** L'Opérateur clique, s'identifie sur Discord (autre fenêtre) ; le serveur du wizard enregistre son compte. */
async function connecterDiscord(user: ReturnType<typeof userEvent.setup>, serveur: ReturnType<typeof fauxServeur>, compte = { id: "111111111111111111", username: "thomas" }) {
  await user.click(screen.getByRole("button", { name: /Se connecter avec Discord|Réessayer la connexion/ }));
  await waitFor(() => expect(serveur.fenetre.location.href).toContain("discord.com"));
  serveur.discord = compte;
  await screen.findByText(/Connecté en tant que/, undefined, { timeout: 4000 });
}

async function parcourirJusquAuRecap(user: ReturnType<typeof userEvent.setup>, serveur: ReturnType<typeof fauxServeur>) {
  await deverrouillerEtCommencer(user);
  await remplir(user, { "Adresse publique": saisies["Adresse publique"] });
  await user.click(screen.getByRole("button", { name: "Continuer" }));
  await remplir(user, {
    "Client ID": saisies["Client ID"],
    "Client Secret": saisies["Client Secret"],
    "Identifiant du serveur Discord": saisies["Identifiant du serveur Discord"],
  });
  await connecterDiscord(user, serveur);
  await user.click(screen.getByRole("button", { name: "Continuer" }));
  // Étape « Options » : tout est facultatif, on continue sans rien toucher.
  await user.click(screen.getByRole("button", { name: "Continuer" }));
}

const RECAP_UCHUU = {
  variables: [
    { nom: "SHAMELESS_BOT_URL", composant: "Shameless", role: "Adresse publique du bot.", valeur: "https://bot.example.org" },
    { nom: "SHAMELESS_BOT_API_KEY", composant: "Shameless", role: "Clé envoyée par l'API au bot.", valeur: "a".repeat(64) },
    { nom: "SHAMELESS_BOT_API_KEY", composant: "Uchuu", role: "Clé envoyée par l'API au bot.", valeur: "a".repeat(64) },
    { nom: "SHAMELESS_API_URL", composant: "Uchuu", role: "Adresse de l'API de Shameless.", valeur: "https://shameless.example.org/api" },
  ],
  envUchuu: "SHAMELESS_BOT_API_KEY=" + "a".repeat(64) + "\nSHAMELESS_API_URL=https://shameless.example.org/api\n",
  aSaisirAMain: [
    { nom: "SHAMELESS_TEAM1_VOICE_ID", facultatif: false, role: "Salon vocal de l'équipe 1." },
    { nom: "SHAMELESS_CLIPS_CHANNEL_ID", facultatif: true, role: "Salon des clips." },
  ],
  docBot: "Les autres variables du bot sont décrites dans docs/shameless.md du dépôt thomas-bsn/harceleur_bot.",
  verification: "curl -H \"Authorization: Bearer " + "a".repeat(64) + "\" https://bot.example.org/internal/shameless/voice-candidates",
};

async function activerUchuu(user: ReturnType<typeof userEvent.setup>, adresse = "https://bot.example.org") {
  await user.click(screen.getByRole("switch", { name: /bot Discord/i }));
  await user.type(await screen.findByLabelText("Adresse publique du bot"), adresse);
}

describe("wizard d'installation", () => {
  it("demande le code d'accès et explique un code refusé", async () => {
    fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await user.type(await screen.findByLabelText("Code d'accès"), "MAUVAIS");
    await user.click(screen.getByRole("button", { name: "Déverrouiller" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Code incorrect.");
    expect(screen.queryByRole("button", { name: "Commencer" })).not.toBeInTheDocument();
  });

  it("indique comment relancer le wizard quand le code a expiré", async () => {
    fauxServeur({ phase: "locked", codeExpired: true });
    render(<WizardApp />);
    expect(await screen.findByText(/docker compose restart wizard/)).toBeInTheDocument();
  });

  it("affiche le stepper et l'étape courante", async () => {
    fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await deverrouillerEtCommencer(user);
    const etapes = screen.getByRole("list", { name: "Progression" });
    expect(etapes).toHaveTextContent("Adresse");
    expect(etapes).toHaveTextContent("Discord");
    expect(etapes).toHaveTextContent("Options");
    expect(etapes).toHaveTextContent("Récapitulatif");
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Adresse");
  });

  it("valide un champ quand on le quitte, avec le message du serveur", async () => {
    fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await deverrouillerEtCommencer(user);
    await user.type(screen.getByLabelText("Adresse publique"), "bad-adresse");
    expect(screen.queryByText("Erreur serveur sur publicUrl.")).not.toBeInTheDocument();
    await user.tab();
    expect(await screen.findByText("Erreur serveur sur publicUrl.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    expect(screen.getByLabelText("Adresse publique")).toBeInTheDocument(); // reste sur l'étape
  });

  it("calcule et affiche l'URL de callback Discord depuis l'adresse publique", async () => {
    fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await deverrouillerEtCommencer(user);
    await remplir(user, { "Adresse publique": "https://shameless.example.org" });
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    expect(await screen.findByText("https://shameless.example.org/api/auth/discord/callback")).toBeInTheDocument();
  });

  it("masque le Client Secret, avec un bouton pour l'afficher, et déplie l'aide « Où trouver ça ? »", async () => {
    fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await deverrouillerEtCommencer(user);
    await remplir(user, { "Adresse publique": saisies["Adresse publique"] });
    await user.click(screen.getByRole("button", { name: "Continuer" }));

    const secret = screen.getByLabelText("Client Secret");
    expect(secret).toHaveAttribute("type", "password");
    await user.click(screen.getByRole("button", { name: "Afficher le Client Secret" }));
    expect(secret).toHaveAttribute("type", "text");

    expect(screen.queryByText(/Reset Secret/)).not.toBeInTheDocument();
    await user.click(screen.getAllByRole("button", { name: /Où trouver ça/ })[1]);
    expect(screen.getByText(/Reset Secret/)).toBeInTheDocument();
  });

  it("récapitule la configuration sans révéler le secret, avec le rappel de sauvegardes", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await parcourirJusquAuRecap(user, serveur);
    expect(await screen.findByRole("heading", { name: "Récapitulatif" })).toBeInTheDocument();
    expect(screen.getByText(saisies["Client ID"])).toBeInTheDocument();
    expect(screen.queryByText(saisies["Client Secret"])).not.toBeInTheDocument();
    expect(screen.getByText(/sauvegard/i)).toBeInTheDocument();
  });

  it("installe puis propose d'ouvrir le site à l'adresse publique", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await parcourirJusquAuRecap(user, serveur);
    await user.click(await screen.findByRole("button", { name: "Installer" }));

    expect(await screen.findByText(/Installation en cours/)).toBeInTheDocument();
    expect(serveur.installations[0]).toMatchObject({ publicUrl: saisies["Adresse publique"], clientId: saisies["Client ID"] });

    serveur.etat = { phase: "done", publicUrl: saisies["Adresse publique"] };
    const lien = await screen.findByRole("link", { name: "Ouvrir Shameless" }, { timeout: 4000 });
    expect(lien).toHaveAttribute("href", saisies["Adresse publique"]);
    expect(screen.getByText(/sauvegard/i)).toBeInTheDocument();
  });

  it("explique une erreur refusée à l'installation et ramène au champ concerné", async () => {
    const serveur = fauxServeur();
    serveur.reponseInstall = { status: 422, json: { errors: { clientSecret: "Discord refuse ce Client ID / Client Secret." } } };
    const user = userEvent.setup();
    render(<WizardApp />);
    await parcourirJusquAuRecap(user, serveur);
    await user.click(await screen.findByRole("button", { name: "Installer" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Discord refuse ce Client ID / Client Secret.");
    await user.click(screen.getByRole("button", { name: "Corriger" }));
    expect(await screen.findByLabelText("Client Secret")).toHaveValue(saisies["Client Secret"]);
    expect(screen.getByText("Discord refuse ce Client ID / Client Secret.")).toBeInTheDocument();
  });

  it("montre l'échec du démarrage et permet de réessayer sans tout ressaisir", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await parcourirJusquAuRecap(user, serveur);
    await user.click(await screen.findByRole("button", { name: "Installer" }));
    serveur.etat = { phase: "failed", publicUrl: saisies["Adresse publique"], failure: "L'API n'a pas démarré. docker compose logs api" };
    expect(await screen.findByText(/L'API n'a pas démarré/, undefined, { timeout: 4000 })).toBeInTheDocument();

    serveur.etat = { phase: "configuring" };
    await user.click(screen.getByRole("button", { name: "Réessayer" }));
    expect(await screen.findByRole("heading", { name: "Récapitulatif" })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("button", { name: "Installer" })).toBeEnabled());
  });

  it("reprend l'écran d'installation quand la page est rechargée en cours d'installation", async () => {
    fauxServeur({ phase: "installing", publicUrl: "https://shameless.example.org" });
    render(<WizardApp />);
    expect(await screen.findByText(/Installation en cours/)).toBeInTheDocument();
  });
});

describe("étape Options (#71)", () => {
  const versOptions = async (user: ReturnType<typeof userEvent.setup>, serveur: ReturnType<typeof fauxServeur>) => {
    await deverrouillerEtCommencer(user);
    await remplir(user, { "Adresse publique": saisies["Adresse publique"] });
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    await remplir(user, {
      "Client ID": saisies["Client ID"],
      "Client Secret": saisies["Client Secret"],
      "Identifiant du serveur Discord": saisies["Identifiant du serveur Discord"],
    });
    await connecterDiscord(user, serveur);
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    await screen.findByRole("heading", { name: "Options" });
  };

  it("propose Europe/Paris, une clé Riot masquée et facultative, et le bot Discord désactivé", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await versOptions(user, serveur);
    expect(screen.getByRole("listitem", { current: "step" })).toHaveTextContent("Options");
    expect(screen.getByLabelText("Fuseau horaire")).toHaveValue("Europe/Paris");
    expect(screen.getByLabelText("Clé de l'API Riot (facultatif)")).toHaveAttribute("type", "password");
    expect(screen.getByRole("switch", { name: /bot Discord/i })).not.toBeChecked();
    expect(screen.queryByLabelText("Adresse publique du bot")).not.toBeInTheDocument();
  });

  it("permet de continuer avec toutes les options désactivées et le dit dans le récapitulatif", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await parcourirJusquAuRecap(user, serveur);
    expect(await screen.findByRole("heading", { name: "Récapitulatif" })).toBeInTheDocument();
    expect(screen.getByText(/Riot/)).toBeInTheDocument();
    expect(screen.getByText(/Ignoré/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Installer" }));
    await waitFor(() => expect(serveur.installations).toHaveLength(1));
    expect(serveur.installations[0]).toMatchObject({ riotKey: "", timezone: "Europe/Paris", uchuu: false });
  });

  it("valide la clé Riot à la saisie avec le message du serveur, sans bloquer si elle est vide", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await versOptions(user, serveur);
    await user.type(screen.getByLabelText("Clé de l'API Riot (facultatif)"), "bad-cle");
    await user.tab();
    expect(await screen.findByText("Erreur serveur sur riotKey.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    expect(screen.getByRole("heading", { name: "Options" })).toBeInTheDocument();
  });

  it("demande l'adresse du bot quand le bot Discord est activé, l'exige, et affiche l'avertissement d'une adresse locale", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await versOptions(user, serveur);
    await user.click(screen.getByRole("switch", { name: /bot Discord/i }));
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    expect(await screen.findByText("Erreur serveur sur botUrl.")).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Options" })).toBeInTheDocument();

    await user.type(screen.getByLabelText("Adresse publique du bot"), "http://localhost:9000");
    await user.tab();
    expect(await screen.findByText(/injoignable depuis le conteneur de l'API/)).toBeInTheDocument();
  });

  it("rappelle l'avertissement d'adresse locale du bot dans le récapitulatif, avant d'installer", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await versOptions(user, serveur);
    await activerUchuu(user, "http://localhost:9000");
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    expect(await screen.findByRole("heading", { name: "Récapitulatif" })).toBeInTheDocument();
    expect(screen.getByText(/injoignable depuis le conteneur de l'API/)).toBeInTheDocument();
  });

  it("explique quoi faire après un échec : corriger puis réessayer, sans commande à lancer", async () => {
    fauxServeur({ phase: "failed", publicUrl: "https://shameless.example.org", failure: "L'API n'a pas démarré." });
    render(<WizardApp />);
    expect(await screen.findByText(/relue automatiquement/)).toBeInTheDocument();
  });

  it("affiche à la fin un seul bloc Bot Discord, une ligne Copier par variable et la doc du bot", async () => {
    const serveur = fauxServeur();
    serveur.recap = RECAP_UCHUU;
    const user = userEvent.setup();
    render(<WizardApp />);
    await versOptions(user, serveur);
    await activerUchuu(user);
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    expect(await screen.findByText(/Activé/)).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Installer" }));

    const bot = await screen.findByRole("region", { name: "Bot Discord" });
    expect(within(bot).getByRole("button", { name: "Copier SHAMELESS_BOT_API_KEY" })).toBeInTheDocument();
    expect(within(bot).getByRole("button", { name: "Copier SHAMELESS_API_URL" })).toBeInTheDocument();
    expect(within(bot).getByText(/docs\/shameless\.md/)).toBeInTheDocument();
    expect(within(bot).getAllByRole("button", { name: /Copier/ }).length).toBe(2);
    expect(screen.queryByText("SHAMELESS_TEAM1_VOICE_ID")).not.toBeInTheDocument();
    expect(screen.queryByText(/voice-candidates/)).not.toBeInTheDocument();
  });

  it("propose Sur internet ou Sur cette machine, et préremplit l'adresse locale sans saisie", async () => {
    fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await deverrouillerEtCommencer(user);
    expect(screen.getByRole("radio", { name: /Sur internet/ })).toBeChecked();
    expect(screen.getByLabelText("Adresse publique")).toBeInTheDocument();

    await user.click(screen.getByRole("radio", { name: /Sur cette machine/ }));
    expect(screen.queryByLabelText("Adresse publique")).not.toBeInTheDocument();
    expect(screen.getByText(window.location.origin)).toBeInTheDocument();

    await user.click(screen.getByRole("radio", { name: /Sur internet/ }));
    expect(screen.getByLabelText("Adresse publique")).toHaveValue("");
  });

  it("désigne le bot par le nom saisi, « Bot Discord » sinon", async () => {
    const serveur = fauxServeur();
    serveur.recap = RECAP_UCHUU;
    const user = userEvent.setup();
    render(<WizardApp />);
    await versOptions(user, serveur);
    await activerUchuu(user);
    await user.type(screen.getByLabelText(/Nom du bot/), "Harceleur");
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    expect(await screen.findByText("Harceleur")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Installer" }));
    expect(await screen.findByRole("region", { name: "Harceleur" })).toBeInTheDocument();
  });

  it("garde la relecture des secrets repliée sur l'écran de fin", async () => {
    fauxServeur({ phase: "done", publicUrl: "https://shameless.example.org", uchuu: RECAP_UCHUU });
    render(<WizardApp />);
    expect(await screen.findByText("Relire les secrets plus tard")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Copier la commande de relecture" })).toBeInTheDocument();
  });

  it("n'affiche aucun bloc bot Discord quand l'intégration est ignorée", async () => {
    fauxServeur({ phase: "done", publicUrl: "https://shameless.example.org" });
    render(<WizardApp />);
    expect(await screen.findByRole("link", { name: "Ouvrir Shameless" })).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Bot Discord" })).not.toBeInTheDocument();
  });
});

describe("étape Discord : premier Superadministrateur (#113)", () => {
  const versDiscord = async (user: ReturnType<typeof userEvent.setup>) => {
    await deverrouillerEtCommencer(user);
    await remplir(user, { "Adresse publique": saisies["Adresse publique"] });
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    await remplir(user, {
      "Client ID": saisies["Client ID"],
      "Client Secret": saisies["Client Secret"],
      "Identifiant du serveur Discord": saisies["Identifiant du serveur Discord"],
    });
  };

  it("n'a plus de champ d'identifiants d'administrateurs et explique la connexion et la seconde URL de redirection", async () => {
    fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await versDiscord(user);
    expect(screen.queryByLabelText(/administrateurs/i)).not.toBeInTheDocument();
    expect(screen.getByText(/premier Superadministrateur/)).toBeInTheDocument();
    expect(await screen.findByText("https://shameless.example.org/_setup/api/discord/callback")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Se connecter avec Discord" })).toBeInTheDocument();
  });

  it("ne laisse pas continuer sans connexion Discord, en l'expliquant", async () => {
    fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await versDiscord(user);
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    expect(await screen.findByText(/Connectez-vous avec Discord pour continuer/)).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Connexion Discord" })).toBeInTheDocument();
  });

  it("ouvre Discord avec la saisie de l'étape, affiche le compte connecté et laisse continuer", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await versDiscord(user);
    await connecterDiscord(user, serveur);
    expect(serveur.demarragesDiscord[0]).toMatchObject({ publicUrl: saisies["Adresse publique"], clientId: saisies["Client ID"], clientSecret: saisies["Client Secret"] });
    expect(screen.getByText(/Connecté en tant que/)).toHaveTextContent("thomas");
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    expect(await screen.findByRole("heading", { name: "Options" })).toBeInTheDocument();
  });

  it("affiche le compte dans le récapitulatif et n'envoie aucun identifiant d'administrateur à l'installation", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await parcourirJusquAuRecap(user, serveur);
    expect(await screen.findByText("thomas (111111111111111111)")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Installer" }));
    await waitFor(() => expect(serveur.installations).toHaveLength(1));
    expect(serveur.installations[0]).not.toHaveProperty("adminIds");
  });

  it("explique un refus ou un échec de connexion et permet de réessayer", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await versDiscord(user);
    await user.click(screen.getByRole("button", { name: "Se connecter avec Discord" }));
    serveur.discordError = "Vous avez refusé l'autorisation sur Discord.";
    const alerte = await screen.findByRole("alert", undefined, { timeout: 4000 });
    expect(alerte).toHaveTextContent("Vous avez refusé l'autorisation sur Discord.");

    // Réessai : la connexion aboutit et l'erreur disparaît.
    serveur.discordError = "";
    await connecterDiscord(user, serveur);
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("n'ouvre pas Discord tant que l'adresse, le Client ID ou le Client Secret sont invalides", async () => {
    const serveur = fauxServeur();
    const user = userEvent.setup();
    render(<WizardApp />);
    await deverrouillerEtCommencer(user);
    await remplir(user, { "Adresse publique": saisies["Adresse publique"] });
    await user.click(screen.getByRole("button", { name: "Continuer" }));
    await user.click(screen.getByRole("button", { name: "Se connecter avec Discord" }));
    expect(await screen.findByText("Erreur serveur sur clientId.")).toBeInTheDocument();
    expect(serveur.demarragesDiscord).toHaveLength(0);
    expect(serveur.fenetre.location.href).toBe("");
  });

  it("propose un lien si le navigateur a bloqué la fenêtre", async () => {
    const serveur = fauxServeur();
    vi.stubGlobal("open", vi.fn(() => null));
    const user = userEvent.setup();
    render(<WizardApp />);
    await versDiscord(user);
    await user.click(screen.getByRole("button", { name: "Se connecter avec Discord" }));
    const lien = await screen.findByRole("link", { name: /Ouvrir Discord/ });
    expect(lien).toHaveAttribute("href", expect.stringContaining("discord.com"));
    expect(serveur.discord).toBeNull();
  });
});
