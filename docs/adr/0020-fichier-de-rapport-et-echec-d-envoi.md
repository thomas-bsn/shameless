# 0020 — Un seul format de Rapport, et une procédure humaine quand l'envoi échoue

Statut : acceptée — 2026-10-19

## Contexte
Le Companion peut ne pas réussir à envoyer un Rapport : API injoignable, Code refusé
(Série terminée ou renouvelé), pas de game en attente. La saisie manuelle reste le secours
(ADR 0004), mais elle n'a pas les données Riot (stats, champions). Il faut un moyen de les
récupérer plus tard sans rendre le Companion plus puissant ni plus dangereux.

## Décision
- Le **Fichier de Rapport** est le corps JSON exact du Rapport envoyé à
  `POST /companion/{code}/rapports`, rien d'autre : un seul format sert l'envoi normal, la
  file de renvoi, le fichier remis à la main et l'import sur le site.
- Le Companion garde le Rapport dans une file locale persistante et le renvoie avec
  backoff (l'API dédoublonne par `riotMatchId`). Sur un refus définitif, il l'abandonne
  de la file mais garde le fichier.
- Quand l'envoi échoue, le Companion affiche une procédure : ouvrir le fichier et le
  donner à un Superadministrateur ou à un Administrateur. Les pseudos Discord de ceux-ci
  viennent d'une route publique de l'API, lus à la connexion et gardés en cache (l'API peut
  être injoignable au moment de l'échec).
- Un Administrateur dépose le fichier sur une Game terminée depuis l'Administration
  (**Import d'un Rapport**). Même validation que le Rapport, plus : participants, Sides et
  vainqueur identiques à la Game, sinon refus complet avec la liste des écarts. L'import
  ne change jamais le vainqueur ni la composition ; il recalcule les costs.

Alternatives écartées : un webhook Discord dans le Companion (secret copié chez tous les
joueurs) ; un envoi par l'API et le bot (inutilisable quand l'API est injoignable, et le
bot est facultatif) ; un Rapport « en revue » pour une game déjà terminée (le circuit
accepter ou rejeter vaut pour la game en attente, pas pour corriger une game passée).

## Conséquences
- Le Companion se télécharge depuis une GitHub Release du dépôt, via un bouton de l'Écran de préparation. Non signé : Windows avertit au premier lancement, ce que le tutoriel explique.
- Une route publique, sans authentification, expose les pseudos Discord du
  Superadministrateur et des Administrateurs. Une Instance qui voudrait les cacher devra
  la protéger : à rouvrir si les Instances deviennent moins fermées.
- Le Companion n'a aucun lien avec Discord.
- Plus tard, des Administrateurs repérables dans Discord (pseudo en rouge) permettront un
  texte « donne-le à un admin » sans changer la route (Backlog).
