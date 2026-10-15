# 0010 — Liens d'accès à portée limitée pour la Draft de champions

Statut : acceptée — 2026-10-16

## Contexte
L'ADR 0005 fait de Discord OAuth la seule connexion, et toute commande qui modifie une
game ou une série exige un utilisateur connecté. La Draft de champions (#35) fait jouer
les bans et les picks par les deux capitaines, depuis leur navigateur, pendant que le
reste du groupe regarde. Un capitaine n'a pas forcément de compte lié, ou pas sous la
main au moment de la game, et un spectateur n'a aucune raison de se connecter pour
regarder.

## Décision
Trois façons d'accéder à une Série, chacune limitée à ce qu'elle permet :
- **Organisateur** : un utilisateur connecté (ADR 0005). Il ouvre la Draft de
  champions, distribue les liens, les renouvelle, et peut jouer une action à la place
  d'un capitaine dont le temps est écoulé.
- **Lien capitaine** : un lien privé par Équipe, valable pour toute la Série. Il ne
  permet que les bans et les picks de cette Équipe, dans cette Série, et la lecture de
  la Draft en cours. L'organisateur peut le renouveler, ce qui invalide l'ancien.
- **Lien spectateur** : un lien public par Série, en lecture seule. Il n'est
  disponible qu'une fois que l'organisateur a indiqué avoir envoyé les liens capitaine.

Un lien est un secret aléatoire long, porté par l'URL. L'API le garde pour pouvoir le
réafficher à l'organisateur, ne l'expose à personne d'autre (les capitaines ne voient
pas le lien de l'autre Équipe, les spectateurs aucun lien capitaine), et ne crée ni
session ni cookie à partir d'un lien. Un lien ne donne aucun accès au compte,
aux autres Séries ni aux autres commandes.

Alternatives écartées :
- Exiger la connexion Discord des capitaines : bloque un capitaine sans compte lié et
  ralentit le début de chaque game.
- Un seul lien partagé pour les deux capitaines : chacun pourrait jouer pour l'autre
  Équipe.

## Conséquences
- Seule exception à l'ADR 0005 : les commandes de la Draft de champions acceptent un
  lien capitaine, et sa lecture accepte un lien spectateur. Tout le reste exige
  toujours un utilisateur connecté.
- La capacité d'envoi limitée du Companion (#36) suivra le même principe : une portée
  étroite, révocable, sans accès au compte.
- Un lien capitaine partagé par erreur se règle en le renouvelant.
