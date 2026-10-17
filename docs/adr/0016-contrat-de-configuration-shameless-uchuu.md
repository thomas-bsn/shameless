# 0016 — Contrat de configuration Shameless ↔ Uchuu

Statut : acceptée — 2026-10-17

## Contexte
L'ADR 0013 décrit les identités machine entre l'API et le bot Discord (Uchuu, dépôt
`harceleur_bot`). Le wizard (ADR 0015, ticket #71 de la spec #72) doit désormais générer
les secrets, les enregistrer côté Shameless et dire à l'Opérateur quoi reporter dans le bot.
Les noms des variables forment un contrat entre deux dépôts : ils doivent être figés et
testés. Les questions ouvertes de #72 ont été tranchées, puis le point 5 confirmé sur
`harceleur_bot` : le bot appelle l'API avec `SHAMELESS_BOT_TO_API_KEY` et `SHAMELESS_API_URL`.

## Décision
**Variables écrites par le wizard dans `instance.env`** (si Uchuu est activé) :
`SHAMELESS_BOT_URL` (adresse publique du bot saisie), `SHAMELESS_WEB_URL` (adresse publique
de Shameless), `SHAMELESS_BOT_API_KEY`, `SHAMELESS_WEBHOOK_SECRET`,
`SHAMELESS_BOT_TO_API_KEY`. Dans tous les cas : `RIOT_API_KEY` (vide acceptée) et
`SHAMELESS_TIMEZONE` (`Europe/Paris` par défaut). Compose interpole ses variables avant
que `instance.env` soit lu : l'entrypoint de l'API recopie donc, après l'avoir chargé,
chacune vers sa variable .NET (`Bot__Url`, `Bot__OutboundApiKey`, `Bot__WebhookSecret`,
`Bot__ApiKey`, `Bot__WebUrl`, `Auth__Riot__ApiKey`, `Instance__TimeZone`) quand elle n'est
pas vide ; un `.env` facultatif reste donc valable pour une Instance sans wizard.

**Variables lues par le bot**, à reporter (bloc `.env` copiable) : les trois secrets avec
les mêmes valeurs que ci-dessus, `SHAMELESS_WEB_URL`, `SHAMELESS_GUILD_ID` (l'ID du serveur
Discord saisi à l'étape Discord) et `SHAMELESS_API_URL`. Le client du bot appelle
`{SHAMELESS_API_URL}/bot/…` et l'API est servie sous `/api` par le proxy : le wizard affiche
donc `<adresse publique>/api`, valeur à reporter telle quelle. Confirmé : le bot configure son
`httpx.AsyncClient` avec `SHAMELESS_API_URL` comme `base_url` et appelle `/bot/…`, et le proxy Caddy
retire `/api` avant de transmettre à l'API. L'exemple de `docs/shameless.md` du bot
(`https://shameless.example`, sans `/api`) ne vaut que pour un bot qui joindrait l'API sans proxy.

**Variables du bot saisies à la main** (listées, jamais générées, jamais demandées) :
`SHAMELESS_TEAM1_VOICE_ID`, `SHAMELESS_TEAM2_VOICE_ID`, `SHAMELESS_ANNOUNCE_CHANNEL_ID`,
`SHAMELESS_VOTE_CHANNEL_ID`, `SHAMELESS_PRONOSTICS_CHANNEL_ID` et `SHAMELESS_CLIPS_CHANNEL_ID`
(facultatives), `SHAMELESS_CLIPS_MAX_BYTES` (à aligner sur Shameless). Le reste (jeton,
intents, `INTERNAL_API_KEY`) renvoie à `docs/shameless.md` du dépôt `thomas-bsn/harceleur_bot`.

**Secrets.** Trois valeurs distinctes de 32 octets (CSPRNG, hexadécimal), générées côté
serveur du wizard une seule fois par session et réutilisées aux relances : le bloc déjà copié
reste valable. Le récapitulatif (avec les valeurs) est renvoyé à la confirmation et par
`state` tant que le wizard tourne, puis disparaît avec lui ; il n'est jamais journalisé. On
relit ensuite les valeurs avec `docker compose exec api cat /instance/instance.env`, commande
affichée sur l'écran de fin et dans le README. L'écran de fin propose aussi un `curl` Bearer
sur `GET {SHAMELESS_BOT_URL}/internal/shameless/voice-candidates` ; le wizard ne l'exécute pas.

**Adresse du bot.** `https://` obligatoire, sans chemin ni identifiants ; `http://` toléré pour
localhost ou un réseau privé, avec l'avertissement « injoignable depuis le conteneur de
l'API ». Aucun appel réseau vers le bot ni vers Discord pour cette étape.

**Code partagé.** Les modules de validation (`validation.ts`), de génération des secrets
(`secrets.ts`) et de récapitulatif (`uchuu.ts`) vivent dans `wizard/src/`, à côté de
`config.ts` qui les appelle : le serveur du wizard est le seul à valider et à générer (ADR
0015 : le front n'a pas de copie des règles, il affiche les messages du serveur). Le front
ne reçoit que le résultat en JSON, dont le type est déclaré dans `web/src/wizard/api.ts`.
Aucune duplication ni dépendance entre le package `wizard/` et `web/`, et l'image du wizard
n'a besoin d'aucun contexte de plus. Ces modules ont d'abord été écrits côté `web/src/lib/wizard/`
(#71, partie indépendante) puis déplacés ici.

**Test de contrat.** `wizard/test/uchuu.test.ts` fige la liste des noms de variables de
chaque côté ; `tests/compose/acceptance.sh` vérifie les sept variables dans `instance.env`,
leur réception par l'API et leur absence des logs.

## Conséquences
- Renommer une variable du contrat impose de mettre à jour `NOMS_VARIABLES_UCHUU`, le compose
  et le bot ; le test de contrat échoue sinon.
- Les secrets vivent en clair dans `instance.env` (voir ADR 0015) et dans l'écran de fin le
  temps de la session du wizard.
- `SHAMELESS_API_URL` se termine toujours par `/api` : c'est la valeur correcte derrière le proxy
  de Shameless, sans correction à prévoir.
- Hors périmètre : modifier Uchuu, joindre Discord, déployer le bot, changer les secrets après
  coup (éditer `instance.env` des deux côtés, puis redémarrer).
