# 0013 — Identités machine du bot Discord

Statut : acceptée — 2026-10-17 (« Début de la Game » révisé par l'ADR 0017)

## Contexte
L'ADR 0005 fait de Discord OAuth la seule connexion des personnes ; l'ADR 0010 y ajoute
des liens à portée limitée, et le Companion a son code de Série. Le bot Discord de
l'Instance (repo `harceleur_bot`, Phase 1d) n'est pas une personne : l'API doit l'appeler
(candidats vocaux du Picker, webhooks d'annonce, de moves et de récap), et la Phase 2
(#59) lui fait aussi appeler l'API (présences vocales par Game, votes, pronostics, dépôt
de clips). Il faut deux credentials machine, révocables par l'env, qui n'ouvrent rien
d'autre que ce dont le bot a besoin.

Il faut aussi fixer le « début » d'une Game : rien dans Shameless ne le signale
explicitement. Le Companion n'envoie qu'un rapport de fin de game ; la Draft de
champions est facultative et se joue après le lancement de la game.

## Décision
**API → bot.** L'API présente `SHAMELESS_BOT_API_KEY` (config `Bot:OutboundApiKey`) en
`Authorization: Bearer` sur `GET /internal/shameless/voice-candidates`. Le navigateur
passe par `GET /soirees/open/picker/candidats-vocaux` (session) et ne voit jamais la clé.
Les webhooks sont signés HMAC-SHA256 avec un secret distinct, `SHAMELESS_WEBHOOK_SECRET`
(`Bot:WebhookSecret`), sur `"{timestamp}." + corps brut`, timestamp frais à chaque
tentative, enveloppe v1 du contrat du bot.

**Webhooks par outbox.** Chaque événement est une ligne `evenements_bot` enregistrée
dans la transaction de la commande qui le produit, puis livrée par un job
(`PublicationWebhooksBotJob`), jamais dans la requête. `event_id` stable, dérivé du sujet :
`teams_formed:series:{id}`, `game_started:game:{id}`, `game_completed:game:{id}` (un seul
événement par type et par sujet, index unique). Livré (200) : il ne part plus ; 409, 408,
429, 5xx ou panne réseau : retenté (5 s, doublé, plafonné à 5 min) ; autre 4xx : abandonné ;
non livré au bout de 24 h : abandonné. Le snapshot est relu à chaque tentative ; l'événement
disparaît avec sa Série ou sa Game (Série annulée, game en attente supprimée, ADR 0009).
- `teams_formed` : à toute création de Série (validation du Picker, création directe,
  Revanche), avant la Draft de champions.
- `captain_link` et `spectator_link` : à toute création de Série, un Lien capitaine par Équipe
  (DM) et le Lien spectateur (salon général) ; contrat et échec de DM dans l'ADR 0017.
- `game_started` : au Début de la Game, la fin de la Draft de champions ; révisé par l'ADR 0017.
- `game_completed` : à sa fin, quelle que soit la source du résultat (saisie, Companion,
  revue acceptée), avec le score de la Série à l'issue de cette Game.

**Début de la Game.** *Révisé par l'ADR 0017.* Ce n'est plus le lancement de la Game dans la
Série (qui porte la Draft de champions) mais la fin de la Draft de champions. `game_started` part alors, la fenêtre de présence du bot s'ouvre, et
les pronostics (#62) se closent à ce même instant. `StartedAt` de `GET /bot/games/{id}` est
vide (`null`) jusqu'à la fin de la draft ; il ne vaut plus `CreatedAt` (le lancement de la Game).

**Bot → API.** Le bot présente `SHAMELESS_BOT_TO_API_KEY` (config `Bot:ApiKey`) en
`Authorization: Bearer` sur les routes `/bot/…`, et seulement sur elles. Ce n'est pas un
schéma d'authentification : l'attribut `[AccesParBot]` (`IAllowAnonymous` + filtre
d'autorisation, comme `[AccesParLien]` et `[AccesParCodeCompanion]`) compare la clé en
temps constant. Le bot n'est donc jamais un « utilisateur connecté » : sa clé n'ouvre
aucune route des Utilisateurs, et une session n'ouvre aucune route du bot. Clé vide :
tout est refusé (401). Seules les opérations dont le bot a besoin y sont ajoutées, au fur
et à mesure des tickets (lecture d'une Game pour #65 ; présences, votes, pronostics et
clips pour la Phase 2). Les routes du bot figurent dans le contrat OpenAPI, tag `Bot`.

**Votes (#60).** La fenêtre des votes s'ouvre à la fin de la Game, c'est-à-dire à la
saisie de son résultat (`CompletedAt`, quelle qu'en soit la source), et se ferme 10 minutes
plus tard ; elle est calculée à la lecture, sans job. Un résultat saisi en retard ouvre
donc les votes en retard. Votent les dix Joueurs (par leur compte Discord lié) et les
Spectateurs que le bot a signalés présents en vocal (`PUT /bot/games/{id}/spectateurs`,
qui remplace l'ensemble) : le bot doit pousser ces présences avant la clôture des votes,
après quoi elles sont refusées (409). Tant que les votes sont ouverts, ni les gagnants ni
le décompte des voix ne sont exposés (site comme bot), seulement le nombre de votants.

**Pronostics (#62).** Aucune présence n'est suivie avant une Game : pronostique tout ID
Discord qui n'est pas lié à un Joueur de la composition courante de la Série. Le MVP et
le DOFD pronostiqués sont validés contre cette composition (la partie n'a pas encore commencé),
le vainqueur est « A » ou « B ». Les pronostics se verrouillent au début de la Game
(ci-dessus, ADR 0017) : ils restent ouverts après « Lancer la draft », et ceux déposés après son
`StartedAt` sont ignorés à l'évaluation. La game en attente dont la partie n'a pas commencé
reste la Game pronostiquable (`GET /bot/series/{id}/pronostics`).

## Conséquences
- Trois secrets distincts, tous optionnels : sans eux l'Instance démarre, aucun
  webhook n'est émis, les candidats vocaux répondent 503 et les routes `/bot` 401.
  Rotation : changer l'env des deux côtés et redémarrer, aucun changement de code.
- Une commande ne dépend jamais de la disponibilité du bot ; les effets Discord arrivent
  avec quelques secondes de retard (intervalle du job, `Bot:Publication:Interval`).
- L'ordre de livraison suit l'ordre des événements, sauf pendant des retries.
- Le bot doit changer son contrat (« le bot n'appelle jamais l'API ») et provisionner
  l'URL de l'API et sa clé : travail côté `harceleur_bot`.
- `AuthorizationTests` classe chaque route par son ouverture (session, lien, code
  Companion, bot) : une route sous `/bot/` sans `[AccesParBot]` fait échouer les tests.
