# 0019 — Le Companion envoie les données brutes du client, l'API les interprète

Statut : acceptée — 2026-10-19

## Contexte
Le Companion (ADR 0004) lit les stats de fin de game dans le client LoL et les envoie à
l'API. Le client donne le champion joué sous forme d'id numérique, alors que le contrat
du Rapport attendait un id Data Dragon en chaîne (`LeeSin`). L'API a déjà un catalogue
Data Dragon (`DataDragonChampionCatalog`). Le Companion tourne sur le PC de chaque
joueur, où il est lourd à mettre à jour.

## Décision
- Le Companion ne convertit, ne calcule et n'interprète rien : il envoie ce que le client
  lui donne (ids numériques, stats brutes), avec le `riotMatchId` et le Side.
- Le champion du Rapport est l'id numérique du client. L'API le convertit avec son
  catalogue. Si elle ne le connaît pas (nouveau champion, catalogue pas encore à jour),
  le Rapport reste en attente et n'est pas appliqué : un champion faux fausserait pour
  toujours les stats par champion.
- Le contrat `JoueurDuRapportRequest.championId` change : id numérique du client, et non
  plus id Data Dragon. L'API continue d'accepter les noms de stats du client (`UPPER_SNAKE`)
  et ceux de match-v5.

Alternatives écartées : embarquer et tenir à jour un catalogue Data Dragon dans le Companion
(une mise à jour à chaque patch, sur chaque PC) ; envoyer l'id numérique en chaîne et
accepter n'importe quoi côté API (champion faux enregistré en silence).

## Conséquences
- Une logique de plus côté API, testable avec les fixtures du spike ; aucune côté Companion.
- Un nouveau champion peut retarder l'application d'un Rapport jusqu'à la mise à jour du
  catalogue ; le Rapport est renvoyé par la file du Companion (ADR 0020).
- Le Companion reste un module de lecture et d'envoi, plus simple à garder à jour quand
  l'API locale du client change.
