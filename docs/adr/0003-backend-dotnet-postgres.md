# 0003 — Garder le backend .NET et PostgreSQL

Statut : acceptée — 2026-10-15

## Contexte
Le backend existant (ASP.NET Core, EF Core, PostgreSQL) est structuré en couches et
contient déjà le métier : joueurs, séries, cost, auth Discord. Il a de la dette (sécurité,
tests, endpoints cassés) mais pas de problème de fond.

## Décision
On garde .NET, EF Core et PostgreSQL. Le projet passe de .NET 8 à .NET 10, et les versions
des packages sont alignées. Les tests d'intégration utilisent Testcontainers avec un vrai
Postgres.

## Conséquences
- On corrige la dette au lieu de réécrire.
- Le Companion, en .NET aussi, peut partager les types de `contracts/`.
