# Spike Companion (#158)

Outil console jetable : se branche sur le client LoL (lockfile, WebSocket LCU) et enregistre,
à la fin d'une game, `/lol-end-of-game/v1/eog-stats-block` et `/lol-match-history/v1/games/{gameId}`
(relancé jusqu'à 10 fois : l'historique peut mettre 5 à 15 s). Aucun lien avec l'app Avalonia (#160).

## Construire l'exécutable Windows (depuis un Mac)

```sh
cd companion/spike
dotnet publish Spike/Spike.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
# -> publish/shameless-spike.exe
```

Copie `publish/shameless-spike.exe` sur le PC Windows.

## Capturer

Lance le client LoL, puis, dans un terminal :

```
shameless-spike.exe
shameless-spike.exe --lockfile "D:\Riot Games\League of Legends\lockfile" --out captures --raw-all
```

Laisse tourner pendant la ou les games ; Ctrl+C pour arrêter. Chaque game donne un dossier
`captures\<date>-game-<gameId>\` avec `eog-stats-block.json` et `match-history-game.json`
(absent si l'historique n'a jamais répondu 200). `captures\events-<date>.jsonl` garde bruts les
événements WebSocket de fin de game (`--raw-all` : tous).

Si rien ne se capture, relis la console : chaque événement (URI, type) et chaque requête (statut HTTP,
essai n/10) y est journalisé. Pistes : lockfile périmé (401), URI d'événement inattendue (la
console la montre : l'outil déclenche sur `/lol-end-of-game/v1/eog-stats-block` ou sur la phase
`EndOfGame` de `/lol-gameflow/v1/gameflow-phase`).

Une capture par type de partie : contre des bots, normale, ranked, custom à dix si possible.

## Anonymiser avant de committer

```
shameless-spike.exe anonymize captures fixtures-anonymes
```

Remplace pseudos, tags, PUUID et ids de compte par des valeurs fictives cohérentes ; stats,
champions et `gameId` restent. Les `.jsonl` (événements bruts) ne sont pas repris. Relis le
résultat (cherche ton pseudo) avant de le mettre dans le dépôt.

## Tests (sans client LoL)

```sh
dotnet test Spike.slnx
```

Un faux serveur LCU local rejoue des JSON d'exemple : seule la plomberie est testée, pas le format réel.
