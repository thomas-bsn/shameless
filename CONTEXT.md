# Shameless — Contexte du domaine

Shameless est une app open source (MIT) pour organiser des parties personnalisées 5v5  
de League of Legends entre amis : former les équipes, jouer des séries, récupérer les  
stats, garder l'historique et voter MVP et DOFD. Chaque communauté héberge sa propre  
instance.

Ce fichier est le vocabulaire du projet. Le code, les issues et les specs utilisent ces  
mots, avec ce sens-là. Un terme nouveau ou qui change de sens est ajouté ici.

## Acteurs

- **Utilisateur** : une personne connectée avec son compte Discord. Seul moyen de  
connexion. Entité `User` (DiscordId, DiscordUsername, AvatarUrl).
- **Joueur** : une personne qui joue les customs, identifiée par son compte Riot  
(RiotId, PUUID). Entité `Player`. Un utilisateur peut lier son compte Discord à un  
joueur ; un joueur peut exister sans utilisateur lié.
- **Spectateur** : un membre du serveur Discord qui regarde une game sans jouer. Il fait  
des pronostics.
- **Instance** : un déploiement de Shameless pour une communauté, avec son serveur  
Discord, sa base de données et sa propre clé Riot.

## Jeu

- **Game** : une partie personnalisée 5v5 jouée par dix joueurs. Entité `CustomGame`.  
Statut : `pending`, `completed`, `error`.
- **Équipe** : les cinq joueurs d'un même camp dans une série. Une équipe garde son  
identité d'une game à l'autre, même si elle change de side.
- **Side** : le camp sur la carte pour une game donnée, `blue` ou `red`. À ne pas  
confondre avec l'équipe. Choisi à la main à chaque game, ou alterné automatiquement  
si demandé.
- **Série** : une suite de games entre les deux mêmes équipes. Entité `BestOfSeries`.  
Formats : BO1, BO2, BO3, BO5. Une série finit **gagnée** (une équipe atteint le nombre  
de victoires du format), **nulle** (BO2 terminé à 1-1) ou **interrompue**.
- **Interrompre une série** : arrêter une série en cours qui a au moins une game  
terminée, sans vainqueur. Ses games terminées restent et comptent (stats, cost, votes,  
clips).
- **Annuler une série** : effacer une série en cours qui n'a aucune game terminée,  
typiquement créée par erreur. Comme si elle n'avait jamais existé. Une série terminée  
ou interrompue ne peut pas être annulée.
- **Revanche** : une nouvelle série lancée avec les mêmes équipes, sans repasser par  
le Picker. Le format peut changer.
- **Remplacement** : un joueur d'une équipe est remplacé par un autre en cours de série.  
L'équipe et la série restent les mêmes ; la composition est connue game par game.
- **Fearless** : option d'une série. Un champion joué dans une game ne peut plus être  
joué dans les games suivantes de la série, par aucune des deux équipes. L'app affiche  
les champions déjà joués, elle n'empêche rien.
- **Soirée** : une session de jeu qui regroupe des séries. Toute série appartient à une  
soirée. Une seule soirée ouverte à la fois par instance. Ouverte à la main, fermée à la  
main ou automatiquement après une période sans game. Peut passer minuit. Son état est  
sauvegardé côté API.
- **Récap** : le résumé d'une game terminée (vainqueur, stats des dix joueurs, votes),  
affiché sur le site et posté par le bot.

## Former les équipes : le Picker

- **Picker** : l'écran qui transforme dix joueurs choisis en deux équipes, par Draft  
ou par Roulette.
- **Draft** : deux capitaines choisissent les joueurs à tour de rôle, un par un. Les  
capitaines sont désignés à la main, au hasard, ou ce sont les deux plus gros costs.  
Ils font partie des dix joueurs. Le premier à choisir est tiré à pile ou face ou  
désigné à la main.
- **Roulette** : tirage des équipes, en deux variantes au choix. **Aléatoire** :  
hasard pur. **Équilibrée** : tirage au hasard parmi les compositions dont les équipes  
ont un cost total proche ; relancer donne d'autres équipes.
- **Autorole** : étape facultative après la formation des équipes. Attribue  
automatiquement un rôle (top, jungle, mid, ADC, support) à chaque joueur, en deux  
variantes au choix. **Hors rôle** : personne ne joue son rôle principal. **Aléatoire** :  
hasard pur. Ne forme pas les équipes.
- **Draft de champions** : avant chaque game, les deux capitaines bannissent puis  
choisissent cinq champions pour leur Équipe, dans l'ordre fixe du mode Tournament.  
Les champions sont choisis pour l'Équipe, pas pour un joueur : qui joue quoi se  
décide en vocal. À ne pas confondre avec la **Draft**, qui forme les équipes.
- **Capitaine** : le joueur qui mène une Équipe dans une Draft ou une Draft de  
champions. Ceux de la Draft le restent pour la Draft de champions ; après une  
Roulette, l'organisateur en désigne un par Équipe.
- **Lien capitaine** : lien privé qui permet à un capitaine de jouer la Draft de  
champions de son Équipe pendant toute la Série, sans se connecter.
- **Lien spectateur** : lien public, en lecture seule, pour regarder les Drafts de  
champions d'une Série.

## Niveau des joueurs

- **Rang SoloQ** : le rang classé d'un joueur en solo, lu via l'API Riot.
- **Peak** : le meilleur rang qu'un joueur déclare avoir atteint (tier, division,  
saison).
- **MMR** : score interne calculé à partir du peak, puis ajusté par le winrate du mois  
en customs. Ce n'est pas un Elo.
- **Cost** : la valeur d'un joueur pour équilibrer les équipes, de 1 à 6, affichée en  
tiers D, C, B, A, S, SS. Dérivé du MMR, recalculé après chaque game, remis à zéro  
chaque mois (`PlayerMonthlyCost`).
- **Note de performance** : la note de 0 à 100 d'un joueur pour une game, calculée depuis  
ses stats envoyées par le Companion. Chaque critère (KDA, KP, CS, dégâts, vision,  
objectifs) est noté par rapport aux références du rôle joué, puis pondéré selon ce rôle  
(ADR 0011). Elle ne tient pas compte du résultat de la game.

## Votes et titres

- **MVP** : le meilleur joueur d'une game, élu par les dix joueurs. Titre affiché :  
Roi ou Reine.
- **DOFD** : « Dick of the Day », le joueur le plus nul ou le plus toxique d'une game,  
élu par les dix joueurs. Illustré par une bouse de cheval.
- **Honte** *(à valider)* : mention attribuée automatiquement par les stats (pire KDA,  
plus de morts, plus faible participation aux kills), sans vote.
- **Hall de la Honte** : classement du cumul des DOFD (et des Hontes, si validées).
- **Pronostic** : le pari d'un spectateur avant la game sur l'équipe gagnante, le MVP  
et le DOFD. Un bon pronostic rapporte des points.
- **Doyen** *(à valider)* : le spectateur qui a le plus de points de pronostic sur le  
mois. Sa voix compte double aux votes MVP et DOFD.

## Modes de jeu

- **Mode classique** : une série normale.
- **Undercover** : un traître est tiré au sort dans chaque équipe et prévenu en  
message privé. Son objectif est de faire perdre son équipe sans se faire repérer. À  
la fin, chaque équipe vote pour désigner son traître.
- **Mode Proxchat** : la série se joue avec le chat vocal de proximité activé.

## Logiciels

- **Web** : l'interface Shameless (Vite, React).
- **API** : le backend Shameless (.NET). Source de vérité des données.
- **Companion** : l'app PC qui lit les stats de fin de game dans le client LoL et les  
envoie à l'API. Un seul joueur équipé par game suffit.
- **Code Companion** : le secret long, propre à une Série, que le joueur saisit dans le  
Companion pour lui associer cette Série, sans connexion. Il ne sert qu'à envoyer les  
rapports de sa Série, tant qu'elle est en cours : il ne vaut ni Lien capitaine, ni Lien  
spectateur, ni commande d'organisateur. L'organisateur le lit et le renouvelle sur la  
page de la Série ; l'ancien est alors refusé. Porté par la Série (`BestOfSeries.CodeCompanion`).
- **Rapport** : ce que le Companion envoie avec le Code Companion à la fin d'une game :  
l'identifiant Riot stable de la partie et, pour les dix joueurs, leur PUUID, Side,  
champion joué, victoire et stats brutes. La Série vient du code, jamais du rapport. Un  
rapport **conforme** (joueurs détectés = participants de la game en attente, chacun sur  
son Side, Remplacements compris) termine cette game comme une saisie manuelle, qui reste  
le secours ; la game garde alors ses données Riot et le champion joué de chaque joueur.  
Un rapport qui n'est pas conforme est **en revue**. Sans game en attente, le rapport est  
refusé (`RapportDeGame`).
- **Rapport en revue** : un rapport dont les joueurs détectés ne sont pas la composition  
de la game en attente (participants absents, joueurs inattendus, participants sur l'autre  
Side). Il n'est ni accepté ni rejeté en silence : la game reste en attente et la page de  
la Série montre l'écart. L'organisateur connecté (pas le Code Companion) l'**accepte**  
(la game est terminée avec ce rapport ; l'Équipe gagnante est celle des participants  
détectés sur le Side gagnant) ou le **rejette** (la game attend un autre rapport ou une  
saisie manuelle ; le même rapport renvoyé répond « rejeté », sans rien créer). Tant qu'il est en revue, ni game suivante, ni Draft de champions, ni  
saisie manuelle, ni autre rapport (le même renvoyé répond « en revue »). Il disparaît avec  
la game en attente si la Série est interrompue ou annulée (`RapportCompanion`).
- **Bot** : le bot Discord global, dans un repo séparé. Pour Shameless, il annonce les  
séries, poste les récaps, gère les votes et rattache les clips.
- **Proxchat** : le logiciel de chat vocal de proximité, dans un repo séparé.
- **Clip** : une vidéo d'un moment de game, postée par un joueur dans le salon `#clips`  
de Discord, puis rattachée à la game par le bot.

## Données Riot

- **Clé Personal** : la clé API Riot dont dispose chaque instance. Elle donne les  
rangs SoloQ, mais pas les données des parties personnalisées.
- **Clé Production** : clé qui donne accès à la Tournament API et aux games créées  
avec un tournament code. Demandée, en attente.
- **RiotId** : l'identifiant visible d'un compte Riot, `Nom#TAG`. Peut changer.
- **PUUID** : l'identifiant permanent d'un compte Riot. Sert de clé stable.

