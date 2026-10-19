# 0018 — Plusieurs Comptes Discord pour un même Joueur, sans vérification

Statut : acceptée — 2026-10-19

## Contexte
L'ADR 0005 laisse chaque Utilisateur lier librement son Compte Discord à un Joueur. Le lien
était 1 Joueur ↔ 1 Utilisateur (`Player.UserId`) : un Joueur déjà lié refusait les autres
(409), seul un Administrateur pouvait le réattribuer. Or une même personne joue parfois avec
plusieurs Comptes Discord (principal, secondaire, ancien compte). Le product owner demande
qu'un Compte Riot puisse être lié à plusieurs Comptes Discord, sans aucune vérification.

## Décision
- Un **Utilisateur** est lié à **au plus un Joueur** ; un **Joueur** peut avoir **plusieurs
  Utilisateurs**. La source de vérité est `User.PlayerId`, avec la date du lien
  `User.PlayerLinkedAt`.
- Lier reste ouvert à tout Utilisateur connecté, **sans vérification ni confirmation** :
  plus de 409 « déjà lié à un autre compte », plus de réattribution par un Administrateur.
  Lier un Joueur quand on est déjà lié à un autre Joueur répond 409 : on se délie d'abord.
- Le **Discord principal** d'un Joueur est l'Utilisateur lié depuis le plus longtemps (à
  égalité, le plus petit id). C'est lui qui donne la photo et l'identité affichées partout
  (listes de Joueurs, classements, Hall de la Honte, picker, draft, votes, pronostics, fiche).
  Si le principal se délie, le suivant devient principal.
- Le principal est **dénormalisé** dans `Player.UserId` (donc `Player.User`), recalculé par le
  dépôt à chaque lien et déliaison. Les requêtes d'affichage, qui incluent déjà `Player.User`,
  restent inchangées.
- Chaque Utilisateur lié voit son Joueur sur `/players/me` et sur le Profil, qui liste les
  autres Comptes Discord liés (le principal est marqué) et rappelle que le lien n'est pas
  vérifié.
- Tout ce qui se décide par compte Discord traite **tous** les comptes liés d'un Joueur comme
  ce Joueur : les Joueurs d'une Série ne pronostiquent pas, on ne vote pas pour soi, les
  candidats vocaux du Picker (un Joueur n'est proposé qu'une fois).
- Migration : les liens existants passent côté Utilisateur ; leur date, jamais enregistrée,
  est celle de la migration.

## Conséquences
- **Pas de vérification : n'importe qui peut se lier à n'importe quel Joueur.** Le premier
  lié gagne l'identité affichée : lier un Joueur avant son vrai propriétaire en détourne la
  photo, et le vrai propriétaire ne peut que s'y ajouter en second. Seul un Utilisateur qui
  se délie (jamais un tiers) fait passer le suivant en principal. Ce compromis est accepté
  par le product owner : la communauté est un serveur Discord d'amis.
- Un faux lien donne les droits du Joueur côté Shameless : le Profil permet de régler le
  peak du Joueur et de se délier, `/players/me` donne accès à ses données.
- Le rôle (Administrateur, Superadministrateur) reste attaché à l'Utilisateur (ADR 0017), non
  au Joueur : lier ne donne aucun droit d'administration.
- Le bot Discord reçoit toujours un seul ID Discord par Joueur (le principal) ; il peut ne pas
  reconnaître un autre compte lié du même Joueur.
- La dénormalisation de `Player.UserId` doit rester recalculée par `PlayerRepository`
  uniquement ; ne jamais l'écrire ailleurs.
