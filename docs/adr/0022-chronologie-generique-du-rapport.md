# 0022 — Une chronologie générique dans le Rapport, l'or estimé par l'API

Statut : acceptée — 2026-10-20

## Contexte
La page d'une game doit montrer des courbes dans le temps (spec #196). En custom, Riot ne
donne rien de tel : ni l'API match-v5, ni la timeline, ni les frames, sans clé de production
(ADR 0004). Le seul accès est l'API locale du jeu, que le Companion peut échantillonner
pendant la partie : kills, morts, assists, CS, niveau, score de vision, valeur des objets,
état mort ou vivant, et la date des objectifs. L'or, les dégâts et l'XP n'y sont pas.

## Décision
- Le Rapport accepte une `chronologie` **facultative** : des `points` datés (`t` en secondes
  depuis le début, strictement croissants) avec, par joueur (PUUID), `kills`, `deaths`,
  `assists`, `cs`, `level`, `vision`, `itemsValue` et `dead` ; et des `evenements` datés
  (`dragon`, `herald`, `baron`, `turret`, `inhibitor`) avec le `side` de l'équipe. Un Rapport
  sans chronologie reste valide ; une chronologie mal formée refuse tout le Rapport (400).
  Aucun champ absent n'est lu comme zéro.
- L'API la valide, la garde telle que reçue (colonne `chronologie` de la game, comme les
  données Riot brutes), la conserve dans un Rapport en revue et à l'Import d'un Rapport
  (ADR 0020), et l'expose dans le détail de la game. Un Rapport sans chronologie n'efface
  pas celle que la game garde déjà.
- Le Companion ne calcule rien (ADR 0019). Les séries portent leur **nature** : `reelle`
  (kills, morts, assists, cs, niveau, vision, objectifs), `estimee` (or), `indisponible`
  (dégâts, XP). L'**or estimé** se calcule côté API, par joueur : la valeur des objets de
  chaque point, multipliée par l'or total final du joueur et divisée par sa valeur d'objets
  au dernier point. Sans or final (stats absentes) ou sans objets au dernier point, l'or
  estimé du joueur est null ; sans aucun or estimable, la série `or` est `indisponible`.
- Le format ne dépend pas de la source. Une source exacte (clé de production, timeline
  match-v5) pourra produire la même chronologie avec des séries `reelle` à la place des
  `estimee` ou `indisponible`, sans changer l'écran.

Alternatives écartées : calculer l'or dans le Companion (ADR 0019) ; inventer des dégâts ou
de l'XP à partir des stats finales (une courbe fausse plutôt qu'une courbe grisée) ; une
table de points par game (une chronologie ne se lit et ne s'écrit qu'en bloc).

## Conséquences
- L'or estimé suppose que l'or final est acheté en objets au même rythme que sa valeur : il
  sous-estime l'or non dépensé en cours de partie. C'est signalé « estimé ».
- Les games déjà enregistrées n'ont pas de chronologie ; elle n'apparaît qu'avec un Companion
  à jour (#198) ou un Fichier de Rapport importé.
- Une chronologie compte 2 000 points et 500 événements au plus : de quoi échantillonner
  toutes les 10 s pendant plus de cinq heures.
