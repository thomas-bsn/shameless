# 0017 — Début de la Game signalé par le Companion

Statut : acceptée — 2026-10-19

## Contexte
L'ADR 0013 fixait le début d'une Game à son lancement dans la Série, avant la Draft de
champions, parce que rien d'autre n'était toujours présent. Le Companion détecte
désormais le lancement de la partie dans le client LoL et sa fin. L'organisateur ne doit
plus revenir sur le site entre deux games, sauf pour consulter ou pour un Remplacement.
Or la Draft de champions se joue avant la partie : si `game_started` partait à la
partie, il partirait bien après la Draft, et le Side (le camp bleu choisit en premier)
doit être connu avant elle, alors que le Companion ne le lit qu'après.

## Décision
- **Lancement de la Game** et **Début de la Game** sont deux moments distincts. La Game
  est créée quand l'organisateur clique sur « Lancer la draft » (écran de préparation, le
  bouton n'étant actif que quand les deux Capitaines ont rejoint) et porte la Draft de
  champions.
- Le **Début de la Game** est le signal « partie lancée » du Companion (Code Companion).
  C'est lui qui émet `game_started` et clôt les pronostics, ses fenêtres de présence
  s'ouvrant à ce moment. Sans Companion, l'organisateur le signale à la main.
- Le **Side** n'est plus choisi à la main : alternance automatique, celui de la première
  game tiré à pile ou face. Le Rapport du Companion fait foi après la partie et corrige le
  Side s'il diffère.
- Le **Lien spectateur** est disponible dès la création de la Série (révise l'ADR 0010, qui
  l'attendait de l'envoi des liens capitaine). Le bot envoie les Liens capitaine en DM et
  poste le Lien spectateur dans le salon général ; sans bot, ou si un DM échoue,
  l'organisateur copie les liens à la main.
- La première ouverture de chaque Lien capitaine est mémorisée côté API.

Alternative écartée : garder `game_started` au lancement de la Game. Simple, mais les
pronostics se closeraient avant une Draft de champions qui peut durer, et le signal ne
reflèterait pas le vrai début de partie.

## Conséquences
- `StartedAt` de `GET /bot/games/{id}` ne vaut plus `CreatedAt` : il est vide jusqu'au
  signal du Companion. Les pronostics déposés avant `StartedAt` restent valables.
- Une Game peut exister (en Draft de champions) sans avoir commencé.
- Une route Companion s'ajoute à `[AccesParCodeCompanion]` pour le signal de début ; elle
  n'accepte rien d'autre et refuse sans Game en attente.
- Une Série annulée ou une Game en attente supprimée emporte son `game_started` non livré
  (ADR 0009 inchangé).

## Contrat des liens pour `harceleur_bot`
Deux événements s'ajoutent à l'outbox de l'ADR 0013, même enveloppe v1, même signature, mêmes
règles de livraison. Ils sont enregistrés à toute création de Série (validation du Picker,
création directe, Revanche), seulement si les webhooks sont configurés : sans bot lié, rien
ne part et l'organisateur copie les liens sur l'Écran de préparation.

- `captain_link` : un par Équipe, `event_id` `captain_link:series:{id}:team:{A|B}`. Le bot
  envoie `data.url` en DM au Capitaine.
  ```json
  { "series_id": "12", "team": "A", "team_name": "Équipe A",
    "captain": { "name": "Kuro", "discord_id": "111111111111111111" },
    "url": "https://shameless.example.org/draft/capitaine/{secret}" }
  ```
  `captain.discord_id` vaut `null` si le Joueur n'a pas de compte Discord lié : le bot ne peut
  pas écrire, il le rapporte comme échec (`compte_non_lie`). Tant que l'Équipe n'a pas de
  Capitaine, l'événement n'est pas livré (retenté jusqu'à ses 24 h) : il part dès la
  désignation, avec le Capitaine et le lien du moment de la livraison. Un Capitaine changé
  ou un lien renouvelé après livraison ne renvoie rien : l'organisateur copie le lien.
- `spectator_link` : un par Série, `event_id` `spectator_link:series:{id}`. Le bot poste
  `data.url` dans le salon général.
  ```json
  { "series_id": "12", "url": "https://shameless.example.org/draft/spectateur/{secret}" }
  ```
- `url` est `null` quand `Bot:WebUrl` n'est pas configuré (le bot ne peut alors rien envoyer).
  Les URL portent un secret : ne pas les journaliser.

**Échec de DM.** Quand le DM d'un `captain_link` ne peut pas partir, le bot le rapporte, avec
sa clé bot → API (`Authorization: Bearer`, ADR 0013) :

`POST /bot/series/{series_id}/liens-capitaine/{team}/dm-echec`, corps `{ "raison": "…" }`,
`raison` valant `compte_non_lie`, `dm_ferme` ou `autre`. Réponses : 204 ; 400 (équipe ou
raison inconnue) ; 401 ; 404 (Série inconnue) ; 409 (Équipe sans Capitaine).

L'Écran de préparation affiche l'échec à côté du lien concerné. Il s'efface quand l'organisateur
renouvelle le lien ou change de Capitaine. Un DM réussi ne se rapporte pas. Un échec du poste
du `spectator_link` n'a pas de route : le lien reste copiable.
