# Calage de la note de performance sur dpm.lol

Outil lancé à la main par le mainteneur (ticket #185, spec #168, ADR 0011). À partir du dossier de games de SoloQ (`tools/riot-collecte`, #181) et d'une liste de scores dpm.lol saisie à la main, il apparie chaque ligne à sa game et à son joueur, calcule la note Shameless avec des paramètres, cherche les paramètres qui rapprochent la note du score dpm.lol, puis mesure l'écart sur des games mises de côté. Node 24, aucune dépendance d'exécution.

L'outil ne travaille que sur les deux fichiers qu'on lui donne : il n'appelle jamais l'API Riot et ne lit jamais la clé Riot.

## Usage

```
node src/main.ts --dossier ./soloq --scores ./dpm.csv [--sortie ./parametres.json]
```

| Option | Rôle |
|---|---|
| `--dossier` | dossier de collecte (obligatoire) |
| `--scores` | fichier de scores dpm.lol, `.csv` ou `.json` (obligatoire) |
| `--sortie` | écrit les paramètres ajustés et toutes les mesures dans ce JSON |
| `--parametres` | repart des paramètres d'une sortie précédente plutôt que de ceux de l'API |
| `--ajuster` | `poids`, `references` ou `tout` (défaut) |
| `--iterations` | itérations de la descente de gradient (1500 par défaut) |
| `--test` | fraction des games mises de côté pour le test (0,25 par défaut) |
| `--graine` | graine du tirage apprentissage/test (1 par défaut) |
| `--tolerance-date` / `--tolerance-duree` | écarts tolérés à l'appariement, en secondes (600 et 30 par défaut) |

Le rapport affiche d'abord l'appariement (lignes appariées, ambiguës, sans correspondance, avec la raison de chacune), puis l'écart moyen en points et la fréquence du même ordre dans la game, avant et après ajustement, sur l'apprentissage et sur le test. Seul le test dit si le calage généralise ; ne brancher les paramètres dans l'API (`ReferencesDeNote.cs`) qu'après avoir regardé cette ligne.

## Format du fichier de scores

Une ligne par joueur noté sur dpm.lol (onglet « General » : jusqu'à dix lignes par game, ou seulement le joueur ciblé).

| Colonne | Contenu |
|---|---|
| `date` | début de la game, ISO 8601 (`2026-10-01T20:00:00Z`) ; sans fuseau (`2026-10-01 20:00`), la date est en UTC |
| `duree` | durée de la game : secondes (`1812`) ou `mm:ss` (`30:12`) |
| `champion` | nom du champion ; casse et ponctuation ignorées (`Kai'Sa` = `kaisa`) |
| `kills`, `deaths`, `assists` | K/D/A affichés (alias d'en-tête : `k`, `d`, `morts`, `a`) |
| `score` | score dpm.lol (virgule ou point décimal) |
| `place` | facultatif : place dans la game (1 = meilleur). Si toutes les lignes d'une game en ont une, elle sert à l'ordre ; sinon l'ordre suit le score |

CSV (séparateur `,` ou `;` selon l'en-tête, pas de guillemets, ligne d'en-tête obligatoire) :

```
date,duree,champion,kills,deaths,assists,score,place
2026-10-01 20:00,30:12,Zed,12,3,5,84.5,1
2026-10-01 20:00,30:12,Lulu,1,6,19,52,6
```

JSON : un tableau d'objets avec les mêmes clés (`date`, `duree`, `champion`, `kills`, `deaths`, `assists`, `score`, `place` facultatif).

## Appariement

Une ligne est appariée à une game quand son début est à moins de `--tolerance-date` de la ligne et sa durée à moins de `--tolerance-duree`, puis à un joueur de cette game de même champion et de mêmes K/D/A. L'outil ne devine jamais :

- **sans correspondance** : aucune game à cette date et cette durée, ou game trouvée mais aucun joueur du champion avec ce K/D/A (faute de frappe probable) ;
- **ambiguë** : plusieurs joueurs ou games possibles, ou deux lignes visant le même joueur de la même game.

Ces lignes sont listées avec leur numéro (1 = première ligne de données) et exclues de l'ajustement ; corriger le fichier ou élargir les tolérances et relancer.

## La note calculée

Même mécanique que l'ADR 0011 : moyenne pondérée de scores de critères, 0 jusqu'à `faible`, 50 à `moyen`, 100 dès `excellent`, linéaire entre deux ; critère non mesurable omis et poids renormalisés. Le rôle est lu dans `teamPosition` de match-v5, sinon `individualPosition`, sinon « INCONNU » (références du mid, poids égaux sur les six critères d'origine).

Critères de l'ADR : KDA, participation aux kills, CS/min, dégâts/min, vision/min, objectifs/min. Critères ajoutés pour le calage, à poids 0 au départ :

- `orParMinute` : or gagné par minute ;
- `partDesKills`, `partDesDegats`, `partDeLOr` : part de l'équipe (kills, dégâts aux champions, or), composante relative à la game ;
- composante **victoire** (`poidsVictoire`, par rôle) : score 100 si la game est gagnée, 0 sinon.

Les paramètres par défaut redonnent donc exactement la note actuelle de l'API (références et poids de `ReferencesDeNote.cs`, recopiés dans `src/note.ts`).

## Ajustement et évaluation

L'ajustement minimise l'écart quadratique entre la note et le score dpm.lol par descente de gradient (Adam, gradient par différences finies), rôle par rôle : un rôle absent des données n'est pas touché, et un critère sans effet sur les données garde sa valeur de départ. Les poids restent positifs, Faible < Moyen < Excellent, et les poids sont ramenés à une somme de 100 par rôle. La séparation apprentissage/test se fait par game (jamais une game des deux côtés), tirage reproductible avec `--graine`.

Avec peu de lignes par rôle, mieux vaut ajuster `--ajuster poids` d'abord (peu de paramètres), puis `references` : ajuster tout d'un coup sur quelques dizaines de games par rôle sur-apprend, ce que l'écart sur le test révèle.

Mesures rapportées : l'écart moyen en points (valeur absolue), la fréquence où l'ordre dans la game est exactement le même (games d'au moins deux lignes), et la part des paires de joueurs d'une même game rangées dans le même ordre.

## Tests

```
npm test
```

Jeux de données synthétiques : lecture, appariement (dont ambigu et sans correspondance), note sur cas chiffrés, ajustement qui retrouve des poids et des références connus, évaluation et séparation, et l'outil de bout en bout. `npm run typecheck` demande `npm install` (types Node et TypeScript, en développement seulement).
