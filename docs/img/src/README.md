# Générateurs des schémas

Les schémas d'architecture sont **générés par code** (SVG autonomes avec logos intégrés) pour rester modifiables :

| Script | Produit |
|---|---|
| `linkedin.mjs` | `../linkedin-architecture.svg` (1080×1350, format 4:5) |
| `detail.mjs` | `../architecture-couches.svg` et `../architecture-detaillee.svg` |
| `lib.mjs` | fonctions de dessin partagées |
| `icons/` | logos [Simple Icons](https://simpleicons.org) (licence CC0) ; les marques appartiennent à leurs propriétaires |

```powershell
node docs/img/src/linkedin.mjs
node docs/img/src/detail.mjs
```

Pour obtenir un PNG : ouvrir le SVG dans un navigateur, ou en ligne de commande
`msedge --headless --force-device-scale-factor=2 --window-size=1080,1350 --screenshot=out.png file:///.../linkedin-architecture.svg`.

Les schémas Mermaid de la documentation, eux, sont dans les fichiers `.md` (rendus par GitHub).
