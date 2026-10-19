# Shameless — Contexte du domaine

Shameless est une app open source (MIT) pour organiser des parties personnalisées 5v5  
de League of Legends entre amis : former les équipes, jouer des séries, récupérer les  
stats, garder l'historique et voter MVP et DOFD. Chaque communauté héberge sa propre  
instance.

Ce fichier est le vocabulaire du projet. Le code, les issues et les specs utilisent ces  
mots, avec ce sens-là. Un terme nouveau ou qui change de sens est ajouté ici.

## Acteurs

- **Utilisateur** : une personne connectée avec son compte Discord. Seul moyen de  
connexion. Entité `User` (DiscordId, DiscordUsername, AvatarUrl, bannière, rôle), créée à  
la première connexion.
- **Compte Discord** : ce qu'un Utilisateur présente de lui : pseudo, photo, bannière.  
Ils sont relus depuis Discord à chaque connexion, et à la demande depuis le Profil.
- **Compte Riot** : le compte LoL d'un Joueur, identifié par son RiotId et son PUUID.
- **Lier** : associer son Compte Discord à un Joueur, c'est-à-dire à son Compte Riot.  
**Délier** retire l'association ; le Joueur et ses stats restent.
- **Profil** : la page où un Utilisateur voit et gère son Compte Discord et son Joueur  
lié.
- **Administrateur** : un Utilisateur qui a reçu ce rôle d'un autre Administrateur ou d'un  
Superadministrateur (ADR 0017). Il corrige les games, fixe les costs et les peaks, gère les  
clips du Fond, relance l'actualisation des rangs et nomme d'autres Administrateurs.
- **Superadministrateur** : l'Administrateur défini en ligne de commande ou par le Wizard,  
d'ordinaire l'Opérateur. Aucun Administrateur ne peut lui retirer ses droits ; il en  
existe toujours au moins un.
- **Joueur** : une personne qui joue les customs, identifiée par son compte Riot  
(RiotId, PUUID). Entité `Player`. Un utilisateur peut lier son compte Discord à un  
joueur ; un joueur peut exister sans utilisateur lié.
- **Spectateur** : un membre du serveur Discord qui regarde une game sans jouer. Il fait  
des pronostics ; identifié par le bot comme présent en vocal pendant la game, il vote  
aussi pour son MVP et son DOFD.
- **Instance** : un déploiement de Shameless pour une communauté, avec son serveur  
Discord, sa base de données et sa propre clé Riot.
- **Opérateur** : la personne qui installe et héberge l'Instance de sa communauté.  
Elle n'est pas forcément Utilisateur ni administrateur de l'Instance.
- **Wizard** : l'interface web temporaire, distincte du site, qui guide l'Opérateur  
pour configurer une Instance neuve au premier démarrage de Docker Compose. Protégé par  
un code d'accès à usage unique lu dans ses logs ; l'Opérateur s'y connecte avec Discord pour
devenir le premier Superadministrateur ; il s'arrête après l'installation et  
ne se rouvre jamais (ADR 0015).
- **Uchuu** : le bot Discord de l'Instance (dépôt `harceleur_bot`), intégration facultative.  
Le wizard génère les secrets partagés et affiche les valeurs à reporter dans le bot,  
sans jamais l'appeler (ADR 0016).

## Jeu

- **Game** : une partie personnalisée 5v5 jouée par dix joueurs. Entité `CustomGame`.  
Statut : `pending`, `completed`, `error`.
- **Équipe** : les cinq joueurs d'un même camp dans une série. Une équipe garde son  
identité d'une game à l'autre, même si elle change de side.
- **Side** : le camp sur la carte pour une game donnée, `blue` ou `red`. À ne pas  
confondre avec l'équipe. Alterné automatiquement d'une game à l'autre ; celui de la  
première game est tiré à pile ou face. Il n'est jamais choisi à la main : le Rapport du  
Companion fait foi après la partie.
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
- **Page de la Série** : le récap d'une Série, à lien stable et joignable à tout moment :  
format et options, score, les deux Équipes, la liste des Games avec leur vainqueur. Elle  
ne montre ni Capitaine, ni lien, ni code, ni commande.
- **Écran de préparation** : l'écran réservé à l'organisateur, affiché juste après la  
création de la Série. Il montre les Équipes, affiche les Capitaines en lecture seule (ils se  
désignent dans le Picker), donne les deux Liens capitaine, le Lien spectateur et le Code Companion,  
et porte le seul bouton « Lancer la draft », grisé tant que les deux Capitaines n'ont pas  
rejoint. Il se rouvre depuis l'Espace organisateur, sans ce bouton une fois la Draft de  
champions lancée.
- **Espace organisateur** : l'écran de pilotage d'une Série en cours, réservé à  
l'organisateur connecté : game en cours, Rapport en revue, Remplacement, Interruption, accès  
à l'Écran de préparation.

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
Roulette, l'organisateur en désigne un par Équipe dans le Picker (étape « Capitaines »). Un joueur  
ne peut pas être Capitaine des deux Équipes. Le Capitaine n'est affiché ni sur la Page de  
la Série ni sur le récap du Picker.
- **Capitaine rejoint** : un Capitaine a rejoint dès que son Lien capitaine a été ouvert  
une première fois ; la mémoire en est conservée côté API.
- **Lien capitaine** : lien privé qui permet à un capitaine de jouer la Draft de  
champions de son Équipe pendant toute la Série, sans se connecter. Le bot l'envoie en DM  
au Capitaine ; sans bot, ou si le DM échoue, l'organisateur le copie à la main.
- **Lien spectateur** : lien public, en lecture seule, pour regarder les Drafts de  
champions d'une Série. Disponible dès la création de la Série ; le bot le poste dans le  
salon général.

## Niveau des joueurs

- **Rang SoloQ** : le rang classé d'un joueur en solo, lu via l'API Riot.
- **Rôle principal** : la voie qu'un joueur a le plus jouée sur ses 15 dernières parties  
de SoloQ, lue via l'API Riot. Un Administrateur peut relancer son calcul et celui des  
Rangs, pour un joueur ou pour tous.
- **Flex** : la file classée en groupe (`RANKED_FLEX_SR`), distincte de la SoloQ. Son  
rang actuel est lu via l'API Riot et affiché à part ; il n'entre ni dans le cost ni  
dans le MMR.
- **Peak** : le meilleur rang qu'un joueur déclare avoir atteint (tier, division,  
saison), sans vérification. Il s'affiche avec sa saison dans les classements. Les saisons  
proposées vont de S24 à la saison courante.
- **MMR** : barème interne qui convertit un rang SoloQ en cost de départ. Ce n'est pas  
un Elo.
- **Cost** : la valeur d'un joueur pour équilibrer les équipes, de 1 à 6 au dixième,  
affichée en tiers D, C, B, A, S, SS. Recalculé après chaque game terminée du mois  
depuis le cost de départ du mois, la note de performance moyenne et le winrate, la  
performance pesant plus que le winrate ; il ne bouge qu'à partir de cinq games  
terminées dans le mois, dont au moins une notée : le winrate seul ne le fait pas  
évoluer (ADR 0012).
- **Cost de départ** : le cost fixé par un admin pour le mois, sinon le cost final du  
mois précédent ; sans historique de cost, celui du Rang SoloQ actuel (pas du Peak). Le  
cost n'est jamais remis à zéro.
- **Cost mensuel** : le cost d'un joueur pour un mois, conservé mois par mois dans  
`PlayerMonthlyCost` ; un nouveau mois n'efface pas l'historique.
- **Note de performance** : la note de 0 à 100 d'un joueur pour une game, calculée depuis  
ses stats envoyées par le Companion. Chaque critère (KDA, KP, CS, dégâts, vision,  
objectifs) est noté par rapport aux références du rôle joué, puis pondéré selon ce rôle  
(ADR 0011). Elle ne tient pas compte du résultat de la game.
- **Fiche Joueur** : la page d'un joueur : ses stats de customs sur ses games terminées  
(winrate, KDA, CS/min, KP, dégâts, note de performance moyenne), par champion et par  
rôle joué, l'historique de ses games avec leur note, son compte Discord lié, son cost  
actuel et ses rangs SoloQ et Flex actuels.
- **Classement 5v5** : le classement des joueurs de l'instance sur les customs : par  
winrate sur les games terminées (Séries interrompues comprises), puis par games jouées.  
Un joueur sans game terminée y figure, en fin de classement.
- **Classement SoloQ** : le classement des joueurs de l'instance par rang Riot actuel,  
une liste par file classée (SoloQ et Flex). Le Peak n'y entre pas ; un joueur non  
classé dans une file y figure, en fin de liste.

## Votes et titres

- **Vote** : à la fin d'une game, ses dix joueurs et les spectateurs présents en vocal  
votent dans Discord, pendant 10 minutes, pour au plus un joueur de la game par titre  
(MVP, DOFD), jamais pour eux-mêmes. Voter est facultatif. Les joueurs ayant le plus de  
voix remportent le titre, tous en cas d'égalité. Les voix restent secrètes jusqu'à la  
clôture ; résultats ensuite dans le récap et l'historique, titres comptés sur la fiche du joueur.
- **MVP** : le meilleur joueur d'une game, élu par vote. Titre affiché : Roi ou Reine.
- **DOFD** : « Dick of the Day », le joueur le plus nul ou le plus toxique d'une game,  
élu par vote. Illustré par une bouse de cheval.
- **Honte** *(à valider)* : mention attribuée automatiquement par les stats (pire KDA,  
plus de morts, plus faible participation aux kills), sans vote.
- **Hall de la Honte** : classement historique du cumul des titres de DOFD (votes clos de toutes  
les Games terminées, sans remise à zéro mensuelle) ; les ex æquo partagent le même rang (1, 1, 3).
- **Pronostic** : le pari d'un spectateur, dans Discord, sur la prochaine game d'une  
série : l'équipe gagnante, le MVP et le DOFD, chacun facultatif. Les joueurs de la  
série ne pronostiquent pas. Verrouillé au lancement de la game. Chaque choix est jugé  
séparément dès que son résultat est connu (le vainqueur à la fin de la game, le MVP et  
le DOFD à la clôture des votes ; un joueur à égalité parmi les gagnants rend le choix  
juste). Les pronostics justes se lisent dans le récap. Ni points, ni classement, ni  
titre : il n'y a pas de Doyen, et aucune voix ne compte double aux votes.

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
Un rapport qui n'est pas conforme est **en revue** ; un joueur détecté que  
l'Instance ne connaît pas (PUUID inconnu) y est signalé à l'organisateur avec son RiotId. Sans game en attente, le rapport est  
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
séries, poste les récaps, gère les votes et rattache les clips. Il échange avec l'API  
par deux identités machine distinctes (ADR 0013) : l'API l'appelle (candidats vocaux,  
webhooks `teams_formed`, `captain_link`, `spectator_link`, `game_started`, `game_completed`), et il appelle les routes  
`/bot/…` de l'API avec sa propre clé. Ce n'est jamais un utilisateur connecté.
- **Lancement de la Game** : sa création dans la Série (`POST /series/{id}/games`), quand  
l'organisateur clique sur « Lancer la draft ». Elle porte alors la Draft de champions.
- **Début de la Game** : le moment où le Companion détecte le lancement de la partie  
dans le client LoL et le signale à l'API. C'est là que part `game_started` et que les  
pronostics se closent (ADR 0017, qui révise l'ADR 0013). Sans Companion, l'organisateur  
le signale à la main.
- **Fin de la Game** : le Rapport du Companion à la fin de la partie ; l'organisateur  
n'a pas à revenir sur le site, sauf pour consulter ou pour un Remplacement entre deux  
games.
- **Proxchat** : le logiciel de chat vocal de proximité, dans un repo séparé.
- **Clip** : une vidéo d'un moment de game, postée dans le salon `#clips` de Discord.  
Le bot la télécharge et la dépose à l'API, qui la conserve sur l'Instance (le lien  
Discord n'est pas la source durable). Elle est rattachée automatiquement à sa Game  
seulement s'il existe une unique **Game plausible** : une Game lancée le jour de  
publication du clip, dans le fuseau de l'Instance, avant sa publication. Sinon le clip  
reste **sans Game**, jusqu'à ce qu'un Utilisateur connecté le rattache. Les clips  
rattachés se lisent sur la page de leur Game (ADR 0014). Entité `Clip`.

## Données Riot

- **Clé Personal** : la clé API Riot dont dispose chaque instance. Elle donne les  
rangs SoloQ, mais pas les données des parties personnalisées.
- **Clé Production** : clé qui donne accès à la Tournament API et aux games créées  
avec un tournament code. Demandée, en attente.
- **RiotId** : l'identifiant visible d'un compte Riot, `Nom#TAG`. Peut changer.
- **PUUID** : l'identifiant permanent d'un compte Riot. Sert de clé stable.

