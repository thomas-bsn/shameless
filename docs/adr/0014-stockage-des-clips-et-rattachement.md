# 0014 — Stockage des clips et rattachement à leur Game

Statut : acceptée — 2026-10-17

## Contexte
La Phase 2 (#59, #63) fait conserver par Shameless les vidéos publiées dans le salon
`#clips` de Discord et les montre sur la page de leur Game. Le lien Discord d'une pièce
jointe expire et ne peut pas être la source durable. Rien ne dit à quelle Game une vidéo
appartient : seul le moment de sa publication est connu. La spec #59 cite à tort
l'ADR 0010 pour cette décision ; elle est prise ici.

## Décision
**Dépôt par le bot.** Le bot télécharge chaque vidéo de `#clips` et la dépose sur
`POST /bot/clips` (identité machine bot → API, ADR 0013), en `multipart/form-data` :
`fichier` (la vidéo et son type), `messageId`, `attachmentId`, `auteurDiscordId`
(snowflakes en texte), `auteurNom`, `publieLe` (heure de publication du message, ISO
8601). Le dépôt est idempotent sur `attachmentId` : 201 au premier dépôt, puis 200 avec
le même clip, sans réécrire la vidéo. Types acceptés : `video/mp4`, `video/webm`,
`video/quicktime` ; au-delà de `Clips:MaxFileSizeBytes` (500 Mo par défaut, la limite
Discord Nitro), 413. Les limites de Kestrel (30 Mo) et du multipart (128 Mo) sont
relevées pour cette route seulement, après la vérification de la clé du bot.

**Stockage sur l'Instance.** Les vidéos sont des fichiers dans `Clips:StoragePath`, un
volume nommé du compose (`api-clips` monté sur `/clips`), sous un nom choisi par l'API
(GUID + extension du type) : rien de ce que le bot envoie n'entre dans un chemin. La
base garde les métadonnées (table `clips`). Le site lit la vidéo sur
`GET /clips/{id}/fichier` (session, requêtes de plage) ; pas de CDN ni de stockage objet.

**Game plausible.** Le jour de publication d'un clip est le jour calendaire qui contient
`publieLe` dans le fuseau de l'Instance (`Instance:TimeZone`, `SHAMELESS_TIMEZONE`, IANA,
`Europe/Paris` par défaut ; un fuseau inconnu empêche le démarrage). Une Game est
plausible si son début (son lancement, ADR 0013 : `CreatedAt`) tombe ce jour-là, entre
minuit heure de l'Instance et l'instant de publication inclus, quel que soit son statut.
Le clip est rattaché automatiquement si et seulement s'il y a exactement une Game
plausible ; sinon (aucune, ou plusieurs) il est conservé sans Game. Une Game lancée la
veille ne compte pas, même si la Soirée a passé minuit.

**Rattachement.** Le clip porte sa Game (`game_id`, nullable) et la façon dont il y a
été rattaché (`automatique` au dépôt, `manuel` depuis le site, #64). Si sa Game
disparaît (game en attente d'une Série arrêtée, ADR 0009), le clip reste, sans Game
(`ON DELETE SET NULL`). Les associations ambiguës ne sont jamais devinées : un
Utilisateur connecté les règle depuis le site (#64).

**Rattachement manuel (#64).** Le site liste les clips sans Game (`GET /clips/sans-game`).
Pour un clip, il propose les Games lancées la veille ou le jour de sa publication (fuseau
de l'Instance), avant elle (`GET /clips/{id}/games-candidates`) : la veille couvre le
clip publié après minuit. N'importe quelle Game reste choisissable par son numéro.
`PUT /clips/{id}/game` rattache ou corrige (rattachement `manuel`), `DELETE /clips/{id}/game`
retire : le clip redevient sans Game. Le dernier qui enregistre l'emporte.

Alternatives écartées :
- Garder le lien Discord : il expire, et la spec exclut d'héberger les vidéos ailleurs
  que sur l'Instance.
- Rattacher à la Game la plus proche en temps : une erreur silencieuse pire qu'un clip
  à rattacher à la main.
- Jour en UTC : une Game jouée à 1 h du matin à Paris tomberait la veille.

## Conséquences
- Le volume `api-clips` est à sauvegarder avec la base ; il grossit sans purge.
- Le reverse proxy ne doit pas limiter la taille du corps sous `/api` (Caddy n'en fixe
  pas par défaut).
- Un clip publié après minuit pour une Game de la veille, ou un soir à plusieurs Games,
  reste sans Game jusqu'au rattachement manuel.
- Côté `harceleur_bot` : écouter `#clips`, télécharger les pièces jointes vidéo et les
  déposer avec la clé du bot, en rejouant le dépôt en cas d'échec.
- Un clip porte un état « masqué du Fond » (faux par défaut) : il sort du Fond (`GET /clips/fond`, tirage au hasard côté serveur, session requise) mais reste dans la liste des clips et sur la page de sa Game. Seuls les Administrateurs le changent : `PUT /clips/{id}/masque-du-fond` masque, `DELETE` réaffiche (403 à un non-admin, 401 sans session), depuis l'interrupteur « Dans le Fond / Masqué » du lecteur de l'onglet Clips.
