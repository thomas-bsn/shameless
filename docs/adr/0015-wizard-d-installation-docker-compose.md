# 0015 — Wizard d'installation pour Docker Compose

Statut : acceptée — 2026-10-17

## Contexte
Installer une Instance demandait de remplir un `.env` de nombreuses variables (ADR 0006),
dont plusieurs requises dès le démarrage : le compose refusait de s'interpréter sans
`POSTGRES_PASSWORD` et les `DISCORD_*`, et l'API plante au boot sans configuration Discord
(`ValidateOnStart`). Un Opérateur nouveau ne sait pas quelles valeurs sont indispensables,
où les trouver, ni lesquelles se génèrent (spec #67, ticket #70). Shameless reste neutre
vis-à-vis des hébergeurs (ADR 0008) : la solution doit fonctionner avec n'importe quel
hôte qui sait exécuter le Compose, sans accès à l'API de l'hôte ni au démon Docker.

## Décision
**Un service `wizard` temporaire, et une configuration écrite dans un volume partagé.**
`docker compose up -d` sans `.env` démarre tous les services. Le wizard (image
`shameless-wizard`, serveur Node sans dépendance d'exécution + front React construit avec
les composants shadcn/ui du site, mais distinct de lui) collecte la configuration, puis
écrit dans le volume nommé `instance-config` :

1. `instance.env` : les variables d'environnement de l'Instance, au format `CLE='valeur'`
   sourçable par `sh` ;
2. le marqueur `ready`, écrit en dernier, de façon atomique.

`postgres`, `migrate` et `api` démarrent avec un `entrypoint` qui attend `ready`, charge
`instance.env`, puis lance la commande normale : aucune relance manuelle, aucun
`docker.sock`, aucun `:?` bloquant dans le compose. L'ordre base → migrations → API ne
passe plus par `depends_on` (un `up -d` bloquerait indéfiniment tant que le wizard n'a pas
fini) : `migrate` réessaie jusqu'à ce que la base accepte les connexions, puis écrit dans
un volume `migrate-state` un marqueur portant la version des images, que l'API attend. Le
proxy Caddy sert le wizard tant que `ready` manque, puis redémarre (`entrypoint.sh`) avec
la vraie configuration, dont l'adresse publique.

**Cycle de vie.** Le wizard attend que l'API réponde `Healthy` (jusqu'à 5 minutes), affiche
le succès, puis s'arrête (`restart: "no"`). Au démarrage, il s'arrête aussitôt si `ready`
existe : il ne se relance jamais sur une Instance configurée (sinon, prise de contrôle).
`instance.env` seul, sans `ready` (arrêt entre les deux écritures), ne compte pas comme
installé. En cas d'échec de démarrage, il explique le problème et permet de corriger la
saisie puis de relancer ; les secrets générés (mot de passe de la base, déjà utilisé pour
initialiser le volume) sont conservés pour la durée du processus.

**Valeurs calculées et générées.** Le callback Discord est
`<adresse publique>/api/auth/discord/callback`, `Auth__AllowedReturnOrigins` est l'origine
publique, l'adresse servie par Caddy est le domaine (HTTPS automatique) ou `:80` pour un
accès `http://localhost`. L'adresse publique doit être en HTTPS (HTTP toléré seulement pour
localhost). Le mot de passe Postgres (32 caractères alphanumériques, CSPRNG) est généré.
La validation vit côté serveur seule (le front l'appelle au blur) ; Discord est testé par
un jeton `client_credentials` : un refus (401) bloque avec une explication, une
impossibilité de joindre Discord n'est qu'un avertissement.

**Sécurité d'accès.**
- Un code d'accès de 16 caractères (~78 bits), affiché dans `docker compose logs wizard`,
  en mémoire seulement, à usage unique (il ouvre une session HttpOnly, SameSite=Strict,
  puis ne vaut plus rien), valable 30 minutes. Passé ce délai, `docker compose restart
  wizard` en émet un nouveau. Après 5 essais ratés, le code est remplacé.
- Tant que rien n'est installé, rien d'autre n'est joignable : l'API n'est pas démarrée.
- Une fois l'installation terminée, le code est invalide, la session est supprimée et le
  processus quitte ; une nouvelle session est impossible.
- Pas de journal des requêtes : le wizard n'écrit que des événements sans contenu. Aucun
  secret saisi ou généré n'est loggé (le seul secret affiché est le code d'accès, par
  conception) ; le test d'acceptation le vérifie. L'interface ne relit jamais un secret.
- Le wizard est servi par Caddy en HTTP sur le port 80 et en HTTPS auto-signé (`tls
  internal`) sur le 443, pour que les secrets puissent transiter chiffrés.

## Conséquences
- **Secrets en clair dans un volume.** `instance.env` (secrets Discord, mot de passe de la
  base) est stocké non chiffré dans `instance-config`, lisible par les conteneurs qui le
  montent (fichier 0644, parce que l'API tourne sous un autre utilisateur que le wizard).
  Il remplace le `.env` : à sauvegarder avec les autres volumes, à protéger comme lui. Le
  wizard rappelle les sauvegardes (base, clips, clés, configuration). Les images ne
  contiennent aucun secret.
- **Caddy et l'adresse publique.** Caddy lit son adresse au démarrage : il redémarre donc
  après l'installation, et la page du wizard peut perdre sa connexion (elle invite alors
  à ouvrir l'adresse publique). Pour un domaine, Caddy doit être joignable sur 80 et 443
  (`SHAMELESS_HTTP_PORT=80`, `SHAMELESS_HTTPS_PORT=443`) ; sinon le certificat n'est pas
  émis. Le wizard non configuré est exposé sur ces ports jusqu'à la confirmation : c'est le
  code qui le protège. Le premier accès se fait forcément avant que l'adresse finale soit
  connue, donc en HTTP ou en HTTPS auto-signé.
- **Instances existantes.** Une Instance déjà installée avec un `.env` doit créer
  `instance.env` et `ready` pour ne pas rouvrir le wizard (voir README) ; le changement de
  mot de passe de base casserait sinon l'accès au volume `postgres-data`.
- **Ordre au démarrage.** Le marqueur `migrate-state` est comparé à la version des images
  (`SHAMELESS_VERSION`) ; avec le tag mouvant `latest`, une mise à jour peut laisser l'API
  démarrer un instant avant la fin de la migration (elle redémarre seule si elle échoue).
- **Périmètre #70.** Adresse, Discord, administrateurs et secrets internes. Riot, Uchuu et
  le fuseau (#71) sont ajoutés comme une section de plus (`options` dans
  `wizard/src/config.ts`) et une étape du front ; les modules de validation et de génération
  vivent dans `wizard/src/` (serveur seul à valider, voir l'ADR 0016) ; les réglages avancés restent
  lisibles depuis un `.env` facultatif. La modification de la configuration après coup est
  hors périmètre (en attendant : éditer `instance.env` à la main, puis redémarrer les
  conteneurs).
- **Choix de simplicité.** Un seul administrateur de session à la fois ; pas de reprise
  d'un wizard interrompu après `ready` ; vérification de Discord uniquement par
  `client_credentials`, pas de test de l'appartenance au serveur.
- **Alternative écartée.** Un wizard qui pilote Docker (`docker.sock`, `docker compose up`
  avec un `.env` généré) donnerait la racine de l'hôte à un service exposé, et n'est pas
  disponible chez tous les hébergeurs.
- Le test d'acceptation `tests/compose/acceptance.sh` (job CI `compose-acceptance`) couvre
  le parcours de bout en bout ; la vérification Discord est désactivée pour lui
  (`WIZARD_DISCORD_CHECK=off`), faute de vrai compte.
