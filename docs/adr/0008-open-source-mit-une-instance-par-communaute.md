# 0008 — Open source MIT, une instance par communauté

Statut : acceptée — 2026-10-15

## Contexte
Le projet doit être public pour que d'autres groupes l'utilisent. La clé Riot Personal est
prévue pour un projet perso ou une petite communauté.

## Décision
Le code est publié sous licence MIT. Chaque communauté déploie sa propre instance, avec son
serveur Discord, sa base et sa propre clé Riot Personal. Il n'y a pas d'instance centrale
multi-serveurs.

## Conséquences
- Aucune donnée personnelle, aucun secret et aucun ID propre à une communauté dans le repo :
  tout passe par la configuration.
- L'usage d'une clé Personal par instance reste à confirmer dans les conditions de Riot.
