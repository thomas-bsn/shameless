# Fixtures de l'API locale du jeu (Live Client Data API)

Fichiers **écrits à la main** d'après la documentation Riot (`https://127.0.0.1:2999/liveclientdata/allgamedata`),
sans capture réelle : aucune donnée de joueur réel. Pseudos `JoueurN#TAGx`, champions et valeurs inventés ;
les pseudos correspondent à ceux des fixtures `lcu/` anonymisées, pour pouvoir rattacher les joueurs.

| Fichier | Contenu |
|---|---|
| `custom-5v5/allgamedata-debut.json` | 1 min 35, aucun événement d'objectif |
| `custom-5v5/allgamedata-milieu.json` | 10 min 45 : tourelle, dragon, héraut |
| `custom-5v5/allgamedata-fin.json` | 24 min 58 : plus inhibiteur, baron, tourelle |
| `custom-5v5/allgamedata-format-inattendu.json` | JSON valide mais pas le format attendu |

À vérifier sur un client réel : présence et sens de `price` et `count` des objets, forme de `KillerName`
(pseudo, `Name#TAG` ou sbire), noms `TurretKilled` / `InhibKilled`.
