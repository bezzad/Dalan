# Design

## Decisions

1. **Dark only on public pages.** `main.public` sets its own colour tokens (dark), so the landing looks the same for every visitor, like the reference. Login and app screens keep following the system theme.
2. **Glow with CSS only.** Radial and conic gradients behind the hero and the closing call, blurred; the gradient words use `background-clip: text`. No image file, nothing from another host (rule 7).
3. **Fonts.** No new font file: Persian headlines use Vazirmatn (heavy), English headlines use the system serif, small labels use the system monospace.
4. **Only real commands.** The OS tabs show how to get `dalan.mjs` (the M10 CLI) from the site and create a bot. Nothing for the Layer 2 agent until it exists; the Sites card says "soon". Addresses come from `/api/public/settings` (rule 4).
5. **Menu without a script library.** "Problems we solve" is a `<details>` panel; it closes when an item is chosen.
6. **Data in `pages.ts`, drawing in `public.ts`.** Texts, menu items, commands and the checklist are pure data, tested without a browser.
