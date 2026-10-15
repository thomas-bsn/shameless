# 0002 — Front en SPA Vite + React, sans Next.js

Statut : acceptée — 2026-10-15

## Contexte
L'ancien front utilisait Next.js 16, mais toutes ses pages étaient des composants client.
Le rendu serveur et les routes API de Next n'étaient pas utilisés : le backend .NET fait tout.

## Décision
Le front est une SPA : Vite, React, TypeScript, TanStack Router, TanStack Query,
Tailwind et shadcn/ui. Le replay 3D utilise react-three-fiber. Les appels à l'API passent
par un client TypeScript généré depuis la doc OpenAPI du backend.

## Conséquences
- Le front se déploie comme des fichiers statiques.
- Pas d'aperçu riche quand on colle un lien dans Discord : le bot poste ses propres embeds.
- Les types du front suivent automatiquement ceux de l'API.
- Pas de styles inline : Tailwind et les composants shadcn/ui.
