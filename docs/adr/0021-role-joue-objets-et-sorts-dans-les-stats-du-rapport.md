# 0021 — Le rôle joué, les objets et les sorts voyagent dans les stats du Rapport

Statut : acceptée — 2026-10-20

## Contexte
Une capture réelle de fin de game (spike #158, ADR 0004) montre que le client donne, pour
chaque joueur, un rôle détecté (`detectedTeamPosition`, un par rôle et par équipe, juste pour
Zed en Mid), sept emplacements d'objets, deux sorts d'invocateur, six runes avec leurs deux
arbres et un objet de rôle (`roleBoundItem`, la quête de la voie). Le Rapport n'envoyait que
`puuid`, `side`, `championId`, `victoire` et `stats` : le rôle et les objets étaient jetés. Le
site affichait alors le rôle de l'Autorole ou le rôle principal du joueur, pas celui qui avait
été joué, et notait un Zed 41/3/3 comme Support (65 au lieu de 85).

## Décision
- Le Companion copie dans le `stats` de chaque joueur : `teamPosition` (le
  `detectedTeamPosition` du client), `items` (les sept emplacements, vides tolérés),
  `spell1Id` et `spell2Id`. Il ajoute à `donnees` les objectifs de chaque équipe lus dans
  l'historique du client. Les runes (`PERK0` à `PERK5`, arbres) et `roleBoundItem` sont déjà
  dans les stats.
- Le contrat de l'API ne change pas : `stats` et `donnees` sont des objets libres. Le rôle joué
  passe en tête de l'ordre de l'ADR 0011 (position transmise par la partie, puis Autorole, puis
  rôle principal), parce que l'API lit déjà `teamPosition` dans les stats.
- Le Companion ne convertit rien (ADR 0019) : les identifiants d'objets, de sorts et de runes
  sont ceux du client. Les noms et les icônes viennent du catalogue Data Dragon de l'API.
- Ce que le client ne donne pas n'est pas inventé : ordre d'achat, ordre des compétences,
  phase de lane (la chronologie de l'historique est vide), fragments de runes.
- Un Administrateur corrige le rôle, les objets et les runes comme les autres stats (ADR 0011).

Alternatives écartées : de nouveaux champs au Rapport (changement de contrat pour des valeurs
que `stats` porte déjà) ; garder le rôle de l'Autorole (faux dès que des joueurs échangent leur
voie, ce qui arrive en custom).

## Conséquences
- Les games déjà enregistrées gardent leur rôle de repli ; seules les prochaines parties, avec un
  Companion à jour, portent le rôle joué.
- La qualité de `detectedTeamPosition` pour des humains en custom reste à confirmer en soirée.
- Un Rapport enrichi est plus gros (sept identifiants d'objets de plus par joueur) : négligeable.
