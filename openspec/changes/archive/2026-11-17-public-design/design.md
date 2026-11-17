# Design

## Context

Milestones 1 to 10 are built. The public pages (M9) live in the PWA: `pages.ts` holds texts and pure logic, `public.ts` draws them. `GET /api/public/settings` gives the numbers and, since M10, `site_url` and `bot_api_url`.

## Goals / Non-Goals

**Goals:** a visitor understands in ten seconds what runs where and which way the connection goes; a builder sees the three steps and the one line to change.

**Non-Goals:** a marketing site, a pricing page, server-side rendering, Layer 2 pages.

## Decisions

1. **Inspired, not copied.** We take ideas only: a bold short hero, a "how it works" picture with three boxes, numbered steps with real code, a dark code block, a closing call to action. No text, logo, colour or asset from ngrok. Dalan keeps its green brand and Vazirmatn.
2. **Diagram in HTML and inline SVG icons, not an image.** It flips by itself for right to left (CSS grid with logical order), follows light and dark, needs no image file and no foreign host. Five cells: user, link, Dalan, link, computer. The link to the computer has one arrow head toward Dalan and the label "only outgoing call". One dot moves along each link; `prefers-reduced-motion` stops it.
3. **Addresses from settings.** The steps and the diagram print `bot_api_url` and `site_url` from `/api/public/settings`, never a written domain (rule 4). The code example is the old and the new line of a Bot API address; only the server part changes.
4. **Tokens.** `--space-*`, `--radius-*`, `--text-*` sizes, `--code-bg`, `--code-text`, `--accent`, `--shadow` in `:root`, dark values in the dark block. Old rules keep working; new public parts use the tokens.
5. **Wide public layout.** `main.public` grows to `--page-width` (1080px) with a sticky header; app screens keep 640px.

## Risks / Trade-offs

- [More text to keep in two languages] All texts stay in `pages.ts` and `i18n.ts`; tests check both languages have the same parts.
- [The diagram is small on a phone] It stays one row with short labels; checked at 390px width.
