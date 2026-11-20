# UI design system

Every screen follows this page. The reference is ngrok: dark and clean, neutral greys, one green (owner, 2026-11-18). Change this page first when a rule changes.

## Theme
- Light and dark. The first open follows the device; the theme button switches light and dark only and keeps the choice (CLI-35).
- Colours only from the tokens in `client/public/app.css` (`--bg`, `--surface`, `--line`, `--text`, `--muted`, `--brand`, `--danger`). The dark tokens are set twice (media query and `[data-theme="dark"]`); keep both in sync.
- Nothing from another host: fonts and icons are in the page (rule 7).
- Font: Inter for Latin letters, Vazirmatn for Persian (both OFL, copied from npm by `client/build.mjs`).
- Logo: the Dalan gate (`client/src/logo.ts`), an outer arch around a door. The favicon is the round version (`public/icons/icon.svg`); the app icons are square. The not-found page (CLI-40) shows the gate closed.

## Page frame
- **Header on every page**: logo and "دالان" on one line, a link to `/`, at the start. At the end the same tools (`headerTools`): a round language button (globe and EN or FA), the round theme button, then "Log in" or the user menu. No page drops it; language never sits among the menu links.
- **Public pages**: the full site header, and the footer with the big half-cut wordmark, always at the bottom of the screen.
- **Width**: public pages up to `--page-width`. Inner pages (`main.wide`) up to 960px; a simple list or chat may stay narrow. Below that, a 16px side gutter. No sideways scroll on a phone.
- **Page title row**: Back, the title, then the main action of the page as one primary button at the end (for example "Create bot").

## Cards, forms and lists
- A section is a `card`: 1px line, round corners, `--surface`. One topic per card. On a desktop, settings cards may sit in two columns (`main.cols`).
- A form opens when needed (for example the create form under "Create bot"), not always. Labels above fields; short help under the field in `note`. Two short fields sit side by side on a desktop.
- A list of things is one card of rows (`.rows`): name and small grey detail at the start, status, then small actions (`button.small`) at the end.
- Tokens, keys and commands: monospace code block, dark in both themes, with Copy.

## Buttons
- One height per size: 40px normal, 32px `small`, 36px in the header. The words sit in the middle on both axes (`inline-flex`). Buttons in one row line up with the text beside them; never push one button down with a margin.
- Inside a wide page a button is as wide as its words, never full width. Full width only on the login card.
- One primary (green) button per card or row: the main action. Others are plain. A link button for Back and small inline actions.
- A step that cannot be undone (new token, delete) asks first.

## Toasts
- **Every action the user takes ends in a toast**: saved, created, copied, sent, deleted, and every error. Use `toast(text, kind)` from `client/src/toast.ts`; kind `ok` (green dot), `error` (red dot, stays longer), `info` (grey dot).
- One short sentence, what happened and, if needed, what to do next ("Bot created. Copy the token now.").
- Bottom corner at the end side, above the page; gone after a few seconds or a click. Errors in form fields may also show next to the field.

## Motion
- Small and fast (under half a second). Nothing moves when the visitor asks for reduced motion.

## Language
- Persian (right to left) and English. Use logical CSS properties only (inline-start, inline-end). Latin names inside Persian text go in `<bdi>`.
- Short, plain words.
