# 0005 — Connexion par Discord uniquement

Statut : acceptée — 2026-10-15

## Contexte
Toute la communauté est sur un serveur Discord. Le backend avait déjà un login Discord
OAuth limité à un serveur, puis un JWT.

## Décision
La seule connexion est Discord OAuth, limitée au serveur Discord de l'instance. Chaque
utilisateur lie librement son compte Discord à un joueur (compte Riot) déjà présent dans
les games.

## Conséquences
- Pas de mots de passe à gérer.
- Le JWT ne passe plus dans l'URL de callback, et son expiration est corrigée.
- Les endpoints qui modifient des games ou des séries exigent un utilisateur connecté.
