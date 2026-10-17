# 0015 — Assistant d'installation portable

Statut : acceptée — 2026-10-17

## Contexte

Une nouvelle Instance Shameless demande plusieurs valeurs d'environnement : secrets,
identifiants OAuth Discord, identifiant du serveur, adresse publique, et éventuellement
clé Riot et liaison au bot. Leur rôle et leur source ne sont pas évidents pour un nouvel
Opérateur. Un compose brut oblige aussi à connaître les différences entre le domaine
public, l'adresse d'écoute de Caddy et l'URL de callback Discord.

Shameless est destiné à être hébergé par différentes communautés et différents
fournisseurs. Forgeyard est un PaaS possible parmi d'autres. Les fournisseurs n'offrent
pas tous la même API de déploiement ; un assistant qui ne fonctionne que dans Forgeyard
lierait Shameless à ce fournisseur.

## Décision

Créer un Assistant d'installation sous forme d'interface web temporaire et indépendante
du site Shameless. Il propose deux modes :

- **Hôte Docker** : l'Assistant d'installation configure et lance le déploiement Docker
  Compose sur l'hôte qui l'exécute.
- **Export** : il produit un fichier `.env` et une configuration Docker Compose standard
  que l'Opérateur peut utiliser avec un PaaS ou un autre environnement. Il ne modifie
  aucun compte ni configuration chez un fournisseur.

Le mode Hôte Docker contrôle le moteur Docker local et peut donc contrôler l'hôte. Il est
protégé par un code à usage unique, affiché dans les logs, valable 30 minutes, puis
invalidé après l'installation. L'Assistant d'installation s'arrête après le déploiement.
Le mode Export ne reçoit pas d'accès au moteur Docker. Les fichiers produits par le mode
Export doivent être considérés comme sensibles tant qu'ils contiennent des secrets.

L'Opérateur renseigne une adresse publique en HTTPS et choisit si TLS est terminé par
Caddy ou par un proxy externe. L'assistant calcule l'URL de callback Discord et l'origine
autorisée à partir de cette adresse. Avant le déploiement, il guide l'Opérateur dans le
portail développeur Discord pour configurer OAuth et saisir le Client ID, le Client
Secret, l'ID du serveur et au moins un ID Discord administrateur.

Les secrets internes sont générés automatiquement : mot de passe PostgreSQL, ainsi que
les trois clés machine si l'intégration Uchuu est activée. Le Client Secret Discord, la
clé Riot et les clés machine sont saisis ou révélés comme secrets, jamais écrits dans les
logs. Le fichier `.env` créé sur un hôte Docker est protégé par des permissions limitées ;
en mode Export, le fichier est téléchargeable par l'Opérateur.

La clé Riot et l'intégration Uchuu sont facultatives. Si Uchuu est activé, l'assistant
écrit les valeurs Shameless et fournit un bloc prêt à copier dans la configuration
d'Uchuu ; il ne déploie ni ne modifie le bot. Le fuseau horaire est modifiable et les
autres options conservent les défauts de `.env.example`.

Les images Shameless sont publiées publiquement sur GHCR avec des tags versionnés. Le
wizard propose la version stable par défaut. Les sauvegardes ne sont pas fournies par un
service embarqué ; l'assistant rappelle à l'Opérateur de configurer les sauvegardes chez
son hébergeur ou dans son infrastructure.

## Conséquences

- L'installation ne dépend pas d'une session Discord : elle doit pouvoir configurer
  l'OAuth Discord avant que le site soit disponible.
- Le mode Hôte Docker exige un accès privilégié au moteur ; le code temporaire, HTTPS et
  l'arrêt après installation limitent la fenêtre d'accès.
- Le mode Export reste indépendant des API propriétaires, mais chaque PaaS peut demander
  une adaptation de la configuration Compose ou une saisie manuelle de ses variables.
- La CI Shameless doit construire et publier les images `api`, `migrate` et `proxy` dans
  GHCR. Les images ne contiennent aucun secret.
- Le compose de déploiement utilise les images publiées au lieu de construire les images
  depuis les sources au démarrage.
- L'Assistant d'installation et l'application Shameless ont des durées de vie distinctes :
  l'assistant est retiré après le déploiement, le site et l'API restent en service.
- L'Assistant d'installation doit expliquer la configuration des sauvegardes externes ;
  il ne peut pas garantir leur disponibilité ni leur compatibilité entre fournisseurs.

## Hors périmètre

- Créer l'application OAuth Discord à la place de l'Opérateur.
- Automatiser les réglages d'un PaaS ou d'un service de stockage de sauvegarde.
- Déployer ou reconfigurer Uchuu.
- Gérer les mises à jour après l'installation initiale ; l'Opérateur utilise alors son
  environnement Docker ou PaaS.
