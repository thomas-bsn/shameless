# 0017 — Trois rôles en base, le Superadministrateur se définit en ligne de commande

Statut : acceptée — 2026-10-18

## Contexte
Jusqu'ici, les Administrateurs étaient une liste d'identifiants Discord dans la
configuration de l'Instance (`Auth:AdminDiscordIds`), lue à chaque requête. Pour donner ou
retirer ce droit, l'Opérateur devait modifier la configuration et relancer l'API. On veut
qu'un Administrateur puisse en nommer d'autres depuis le site, sans que ces nominations
puissent jamais retirer ses droits à la personne qui a installé l'Instance.

## Décision
Trois rôles sur l'Utilisateur, stockés en base : **utilisateur**, **administrateur**,
**superadministrateur**.
- Un Administrateur promeut un Utilisateur en Administrateur, ou retire ce rôle à un autre
  Administrateur, depuis l'Administration. Il ne peut ni toucher à un Superadministrateur,
  ni se retirer son propre rôle.
- Un Superadministrateur ne se crée et ne se retire que par une commande de l'image de
  l'API (`superadmin ajouter | retirer | lister <identifiant Discord>`) ou par le Wizard à
  l'installation, qui définit le premier par une connexion Discord. Le site n'a aucun
  moyen de le créer ni de le retirer. Le dernier Superadministrateur ne peut pas être retiré.
- Au premier démarrage après la mise à jour, les identifiants de `Auth:AdminDiscordIds`
  deviennent Superadministrateurs ; ensuite la configuration n'est plus lue pour les droits.
- La ligne Utilisateur est créée à la première connexion (et plus au premier lien de
  compte), pour pouvoir promouvoir quelqu'un qui ne s'est pas encore lié à un Joueur.
- La ligne garde « promu par X, le JJ/MM/AAAA ». Il n'y a pas de journal d'actions.

Alternatives écartées : garder la liste en configuration (pas de gestion depuis le site) ;
un rôle Administrateur créable depuis le site sans Superadministrateur (un Administrateur
pourrait retirer tous les autres, y compris l'Opérateur, sans retour possible).

## Conséquences
- Le Wizard gagne une étape de connexion Discord (contrat de configuration, ADR 0016).
- L'ADR 0005 reste vrai (Discord seul moyen de connexion) ; ce qui change est la source des
  droits Administrateur.
- La première connexion écrit en base ; le pseudo, la photo et la bannière Discord sont
  relus à chaque connexion.
