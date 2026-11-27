# UI design system

Every screen follows this page. The reference is ngrok: clean and simple, neutral greys, one green (owner, 2026-11-18). Its principles only, never its brand or words: few words, much space, no glow or rainbow text (owner, 2026-11-22). Change this page first when a rule changes.

## Theme
- Light and dark. The first open follows the device; the theme button switches light and dark only and keeps the choice (CLI-35).
- Colours only from the tokens in `client/public/app.css` (`--bg`, `--surface`, `--line`, `--text`, `--muted`, `--brand`, `--danger`). The dark tokens are set twice (media query and `[data-theme="dark"]`); keep both in sync.
- Nothing from another host: fonts and icons are in the page (rule 7).
- Font: Inter for Latin letters, Vazirmatn for Persian (both OFL, copied from npm by `client/build.mjs`).
- Small text on public pages (CLI-76): section labels (`.label`) use `--text-label`, 14px (15px in Persian); diagram and tag text (`.frame-label`, `ul.guards`, `.branch .box`, `.tag`, `.soon`) uses `--text-small`, 13px (14px in Persian). Nothing on a public page is smaller; code blocks keep their own size.
- Logo: the Dalan gate (`client/src/logo.ts`), an outer arch around a door. The favicon is the round version (`public/icons/icon.svg`); the app icons are square. The not-found page (CLI-40) shows the gate closed.

## Page frame
- **Header on every page**: logo and "دالان" on one line, a link to `/`, at the start. At the end the same tools (`headerTools`): the round language button (only a globe; one click switches Persian and English, Persian first), the round theme button of the same size (36px), then "Log in" or the user menu. No page drops it; language never sits among the menu links.
- **Connection ring** (CLI-75): no bar and no toast when the live stream is down. A thin `--brand` ring turns around the bot's icon in the chat header, or around the Dalan mark on other screens; with reduced motion it is still and dashed. Hover shows "Connected" or "Connecting…". A hidden `role="status"` text tells screen readers. The page never moves.
- **Header links** (public pages, CLI-42): only Plans, Showcase and Guide. FAQ and Privacy live in the footer only, and so do About us and Contact us (CLI-56).
- **Phone (below 760px)**: the links fold into a round ☰ button after the tools; it opens a panel with one 48px row per link, closed by a link, a click outside or Escape. "Log in or sign up" becomes "Log in"; a long user name is cut with "…". Under 360px the logo stands without the name. Every page fits 320px and 360 by 740 portrait and 740 by 360 landscape, with no sideways scroll.
- **Public pages**: the full site header, and the footer with the big half-cut wordmark. On a short page the footer sits at the bottom of the screen (CLI-73: `margin-block-start: auto`, never fixed); on a long page it follows the content. Under the footer columns, one small grey line `© 2026 Dalan v1.1.0` (CLI-72): this year, and the version from `/api/public/settings` (`/health` is private).
- **Landing page**: only what Dalan does, its features and its plans. No install steps or commands: each plan has "Get started" to its guide, and `/guide` first asks which plan.
- **Width**: public pages up to `--page-width`. Inner pages (`main.wide`) up to 960px; a simple list or chat may stay narrow. Below that, a 16px side gutter. No sideways scroll on a phone.
- **Page title row**: Back, the title, then the main action of the page as one primary button at the end (for example "Create bot").

## Cards, forms and lists
- A section is a `card`: 1px line, round corners, `--surface`. One topic per card. On a desktop, settings cards may sit in two columns (`main.cols`).
- A form opens when needed (for example the create form under "Create bot"), not always. Labels above fields; short help under the field in `note`. Two short fields sit side by side on a desktop.
- A list with ticks is `ul.ticks`: the icon sits in the middle of the first text line (`margin-block-start: calc((1lh - size) / 2)`), so it lines up in both fonts, on one line or many.
- A list of things is one card of rows (`.rows`): name and small grey detail at the start, status, then small actions (`button.small`) at the end.
- Tokens, keys and commands: monospace code block, dark in both themes, with Copy.

## Buttons
- One height per size: 40px normal, 32px `small`, 36px in the header. The words sit in the middle on both axes (`inline-flex`). Buttons in one row line up with the text beside them; never push one button down with a margin.
- Inside a wide page a button is as wide as its words, never full width. Full width only on the login card.
- One primary (green) button per card or row: the main action. Others are plain. A link button for Back and small inline actions.
- Plain buttons have the `--control-line` border (3:1 against the page, WCAG 1.4.11); cards, rows and dividers keep the soft `--line` (CLI-71).
- A step that cannot be undone (new token, delete) asks first.

## Addresses
- Access keys for AI assistants live in the settings area (`/account/keys`, CLI-30, CLI-77); "My bots" and "My apps" have one link to them.
- One address per screen (CLI-51): `/chats`, `/@<username>` (a chat), `/bots` (My bots), `/bots/new`, `/bots/<username>` (one bot's page), `/account` and `/account/<section>` (the settings area, CLI-77), `/admin`, `/apps`. No screen hides behind another's address; reload and Back show the same screen.
- A bot's page shows only that bot's values; a token shows only right after create or New token.

## Chat (like Telegram)
- Message text 14px, line 1.35, time 12px: the `--message-*` tokens (CLI-48).
- The bot's reply keyboard sits under the input, where a phone keyboard would be. Rows exactly as sent; a row's buttons share its width. ⌨ in the input bar hides and shows it (not for a persistent one) (CLI-07).
- Button colours: `success` green, `danger` red, `primary` blue (`--primary`), each with its `--on-*` text colour. Link buttons have a small ↗ (CLI-13).
- The microphone button (Dalan's line `mic` icon, CLI-80) takes Send's place while the input is empty; voices and audio use Dalan's own player (CLI-16, CLI-46).
- 📎, paste and drop open one card above the input; the input is the caption (CLI-47).
- Bot icons are round, cut to fill; no icon shows the first letter (CLI-50).
- Reply: a message's menu (right-click, long press), a double-click or a swipe left. The bar above the input shows the quote with ✕; a bubble that replies shows the quote on top, and a click on it goes to the message (CLI-52).
- Typing `/` opens a list above the input with the bot's commands that start with the typed text, each `/command` and its description, left to right. Click, tap or Enter sends; Tab completes; ↑ ↓ move the mark; Esc closes (CLI-74).
- Enter (CLI-85): with a mouse and keyboard Enter sends and Shift+Enter or Ctrl+Enter adds a line; on a phone or tablet (coarse pointer, read at each key) Enter adds a line and only Send sends; never while an IME composes.
- The box grows with its text to `--compose-max-height` (about six lines, at most 30dvh, like Telegram), then scrolls; its buttons stay their size at the bottom; one row again after a send (CLI-86). Over 3 lines a small expand button (two diagonal arrows) sits in its corner and opens the big editor: the same box moved into a `dialog.modal`, full screen on a phone or under 600px, 80% in the middle otherwise; Send and Close; Esc keeps the text; Ctrl+B and Ctrl+I put `**` and `_` around the selection (CLI-87).
- Each chat keeps its unsent text in this browser (`dalan.draft.<user>.<bot>`); a send removes it, logout removes all (CLI-88).
- A photo, or a JPEG, PNG, GIF or WebP document, shows as a picture; a tap opens a black full-screen viewer with pinch, double tap and pan, Download and Close; Esc closes (CLI-89). SVG stays a download.
- An HTML file is a card (name, size, Open, Download). Open shows it full screen in a frame from the view host (`sandbox="allow-scripts allow-popups"`, SEC-16), with Close, Download and Open in a new tab; on an iPhone a fixed layer inside the safe areas (CLI-90).
- While the bot is offline, a plain bar above the input (red start edge, `--muted` text) says it won't answer now and that messages wait, then show "not delivered". It hides when the bot is online; a suspended bot shows its error instead (CLI-91).
- A pressed inline button shows a moving skeleton; the buttons of its message are locked until the bot answers (CLI-53).
- Bot formatting is drawn from entities, never as HTML: bold, italic, code, spoiler (tap to show), quotes (CLI-54).

## Toasts
- **Every action the user takes ends in a toast**: saved, created, copied, sent, deleted, and every error. Use `toast(text, kind)` from `client/src/toast.ts`; kind `ok` (green dot), `error` (red dot, stays longer), `info` (grey dot).
- One short sentence, what happened and, if needed, what to do next ("Bot created. Copy the token now.").
- Bottom corner at the end side, above the page; gone after a few seconds or a click. Errors in form fields may also show next to the field.

## Motion
- Small and fast (under half a second). Nothing moves when the visitor asks for reduced motion.

## Language
- Persian (right to left) and English. Use logical CSS properties only (inline-start, inline-end). Latin names inside Persian text go in `<bdi>`.
- Short, plain words.
- A title, heading, label or button never ends with a full stop (CLI-44). Sentences in paragraphs, notes and toasts keep theirs.
- Persian is a little formal and uses the common word, never a literal translation (CLI-43). Glossary: «بات» (not «ربات»), «اتصال بات» (not «رله»), «برنامه» (not «اپ»), «برنامه‌ی رابط دالان» (not «ایجنت»), «توسعه‌دهنده» for the builder role, «مدیر» (not «ادمین»), «رمز عبور», «جدید» (not «تازه»), «طرح‌ها» (not «پلن»), «گزارش» (not «لاگ»), «پوسته» (not «تم»), «پرسش‌های متداول», «گفت‌وگو». «می‌» and «ها» always with a zero-width non-joiner.
- English text, titles, meta and SEO never name Iran (sanctions, search engines); say "inside the country" or "when outside links are down". Persian may say ایران (CLI-41).

## Login screen (CLI-62, CLI-36)
- The Dalan gate on top (64px), the name under it, then one card.
- The card opens with the password form. A line "or" separates it from the other ways: SMS (phone icon), email (envelope icon), GitHub (its mark), one equal button each, in the order of CLI-36. The development login is a small link under the card.
- Every icon is drawn in the page (rule 7).

## Guides as steppers (CLI-64 to CLI-66)
- A guide is a list of step cards (`ol.stepper`). The step to do now is open, with Back and Next. A step already done is green with a check and "Done", closed, and opens on a press.
- What is done comes from the server (logged in, builder, a key, a site online, a bot made), never from a guess. A step the server cannot know is done by Next.
- The key step has a button that opens a modal (`dialog.modal`) on the same page: it makes the key and shows the login command once, with Copy. It never leaves the page.
- A visitor on the site guide sees only "Log in first" and a Log in button; after the login the same address shows the steps (CLI-63).

## Navigation and phone (CLI-68, CLI-69)
- Going to another address scrolls to the top; an address with a #part goes to that part. Back and Forward keep the browser's place.
- The viewport is scale 1 and pinch zoom stays; every field is 16px or more, so a phone never zooms on focus.
- Examples never name a personal or company project: `mysite` and `shop` (CLI-67).

## Account: link and merge (CLI-70)
- A way to log in that is not set shows its "Set up" or "Link" action on its method card; the merge card (below) shows on Login and security.
- After the proof finds another account, the merge card comes first: the question, one line per account (what it has, whether it stays), a note, then "Merge" (primary) and "Cancel" (link). A refusal shows its reason and no "Merge" button.

## Dashboard (CLI-81 to CLI-83, CLI-92)
- **One frame** for every signed-in page but a bot's chat, at `/dashboard/...`: the settings frame below with four groups: Workspace (Chats; My bots and My apps for a builder or an admin), Account, Building, and Admin (only for an admin: Overview, Requests, Users, Bots, Showcase, Messages, Settings, Access log, Audit log). The old addresses (`/chats`, `/bots`, `/apps`, `/account/...`, `/admin`) move there in the page. An admin address for anyone else shows "Not found".
- **Workspace pages** keep their own screen and title row inside the frame.
- **Overview** (`.stats`): a grid of cards, each one big number, its name and a grey line, a link to its page.
- **Admin settings**: grouped by service; each a setting row with the name and help in the page language, the key small and grey, the control with its unit and range, "Changed" (with the start value) and Reset, "Needs restart"; a search box on top; a risky setting asks with the old and new value.
- **Second factor** (`.card.totp`): Set up shows the QR on a white square, the key to copy and a code box; then the recovery codes once in two columns, with Copy and Download. An admin has no Turn off; until it is on, the admin pages show only this card.

## Settings (CLI-77, CLI-78)
- **Settings frame**: the site header on top; under it a side menu (240px, at the start side) and the section (up to 720px). Below 760px there is no side menu: `/dashboard` is the menu list, and each page starts with "‹ Dashboard".
- **Sections**, one address each: Profile, Login and security, Notifications, Preferences (group Account); Builder and Access keys (group Building, only for an approved builder or an admin). A new section is one entry in `settings/model.ts` and one in `settings/index.ts`.
- **Side menu**: group titles as `.label` (CLI-76), items as 36px rows with an 18px icon and the name; the current item has `--brand-soft` background; hover has a light `--line` background. On a phone: 48px rows with a › at the end.
- **Breadcrumb**: small grey text, `Settings › <section>`, the first part is a link. Not shown on a phone.
- **Section head**: the title (`--text-title`) and one grey line under it that says what the section holds.
- **Setting row** (`.setting-row`): two columns, 1 : 1.4; start: the row title (16px, 600) and its help (14px grey); end: the control, then its button under it. Rows are split by a 1px `--line`; 32px between rows. Below 760px one column.
- **Row button**: plain by default; primary only while the value changed (so a page never shows five green buttons at once).
- **Status badge** (`.badge.ok`, `.badge.off`): small round-cornered tag; ok is `--brand-soft` with `--brand` text and a check; off is `--line` with `--muted` text.
- **Method card** (`.method.card`): a card per login way: icon, name and badge at the start; the action at the end; under them the details (the email address) as a small row on `--bg`.
- **Radio card** (`.radio-card`): a card with a radio, a small picture and a name; the chosen one has a `--brand` border and `--brand-soft` background. Used for theme, language and calendar (its picture is today's date in that calendar); a choice applies at once.
- **Danger card** (`.card.danger`): a card with a `--danger` border tint, a title, the text, and a plain button with `--danger` text. Every button in it asks first. Profile's "Delete my account" (AUTH-36) is one.
- **Sessions list** (`ul.sessions`, AUTH-35): in Login and security; a row per session: browser · system, "This device" badge, the address and last use, End; "Log out other devices" in the card's row.
- **Empty state** (`.empty`): centred 32px grey icon, a short bold line, one grey line that says how it fills.
- **Grouped menu** (the user menu, and later menus): a head with the name and, in grey, the phone or email; items are 40px rows with an 18px icon; a 1px `--line` between groups; Log out last, alone. Groups: (My bots, Chats, My apps), (Settings, Admin for an admin), (Log out).
- Icons are `glyph(name)` in `client/src/icon.ts`, inline SVG, 24 by 24 grid.
