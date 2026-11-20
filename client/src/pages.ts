// Pure logic and long texts of the public pages (CLI-26 to CLI-29), kept apart so tests run without a browser.
// The owner may change these texts; a lawyer should read them before the public launch (DESIGN section 13).

export type Lang = "fa" | "en";

/** ADM-14: the numbers the pages print. They come from the server, never from this file (rule 4). */
export interface PublicNumbers {
  history_messages: number;
  history_days: number;
  access_log_days: number;
  file_days: number;
  update_expiry_seconds: number;
  /** ADM-15: the addresses, for the ready prompt. */
  site_url: string;
  bot_api_url: string;
}

/** CLI-26: which screen an address shows. Unknown addresses never reach the page: the server answers 404. */
export type Page = "landing" | "privacy" | "faq" | "showcase" | "login" | "bots" | "apps" | "chat";

export function pageFor(path: string, loggedIn: boolean): Page {
  if (path === "/privacy" || path === "/faq" || path === "/showcase") return path.slice(1) as Page;
  if (path.startsWith("/@")) return loggedIn ? "chat" : "login";
  if (path === "/bots") return loggedIn ? "bots" : "login";
  if (path === "/apps") return loggedIn ? "apps" : "login";
  return "landing";
}

/** CLI-26: the three messages of DESIGN section 13, in this order. Never "free tunnel". */
export const landing: Record<Lang, { title: string; text: string }[]> = {
  fa: [
    { title: "برنامه‌تان را از کامپیوتر خودتان اجرا کنید.", text: "سرور نخرید. بات یا برنامه روی کامپیوتر خانه یا محل کارتان می‌ماند و فقط به دالان وصل می‌شود. IP عمومی لازم نیست." },
    { title: "کاربر واقعی، تأییدشده با ایمیل یا پیامک.", text: "هر کاربر دالان با ایمیل یا پیامک تأیید شده است. لازم نیست خودتان سیستم ورود بسازید." },
    { title: "وقتی اینترنت بین‌الملل قطع است، کار می‌کند.", text: "دالان در ایران است. بات شما و کاربرانش بدون اینترنت بین‌الملل هم به هم می‌رسند." },
  ],
  en: [
    { title: "Run your app from your own computer.", text: "Do not buy a server. Your bot or app stays on your home or office computer and only connects out to Dalan. No public IP is needed." },
    { title: "Get real users, verified by email or SMS.", text: "Every Dalan user is verified by email or SMS. You do not need to build a login." },
    { title: "Keep working when international internet is cut.", text: "Dalan runs inside Iran. Your bot and its users reach each other without international internet." },
  ],
};

/** CLI-26: the hero at the top of the landing page. `glow` is drawn in colour on its own line. */
export const hero: Record<Lang, { title: string; glow: string; text: string; how: string }> = {
  fa: {
    title: "بات روی کامپیوتر خودتان،",
    glow: "در دسترس همه‌ی ایران.",
    text: "دالان کاربران تأییدشده را به بات یا برنامه‌ی روی کامپیوتر شما می‌رساند. نه سرور، نه IP عمومی، نه پورت باز.",
    how: "چطور کار می‌کند؟",
  },
  en: {
    title: "Your bot on your computer,",
    glow: "reachable from all of Iran.",
    text: "Dalan brings verified users to the bot or app on your own computer. No server, no public IP, no open port.",
    how: "How does it work?",
  },
};

export interface MenuItem { title: string; text: string; href?: string; soon?: boolean }

/** CLI-26: the "Problems we solve" menu. A live item links to the page; what is not built yet says "soon". */
export const problems: Record<Lang, { group: string; items: MenuItem[] }[]> = {
  fa: [
    { group: "ساخت و تست", items: [
      { title: "اجرای بات از خانه", text: "بات روی کامپیوتر خودتان، بدون سرور و IP عمومی", href: "#how" },
      { title: "بات‌های تلگرامی، بدون تغییر کد", text: "فقط آدرس سرور را عوض کنید", href: "#try" },
      { title: "دستیار هوش مصنوعی", text: "ساخت و مدیریت بات با CLI و MCP", href: "/llms.txt" },
    ] },
    { group: "رساندن به کاربر", items: [
      { title: "کاربر واقعی", text: "همه‌ی کاربران با ایمیل یا پیامک تأیید شده‌اند", href: "#features" },
      { title: "وقتی اینترنت بین‌الملل قطع است", text: "دالان در ایران است", href: "#features" },
      { title: "ویترین بات‌ها", text: "کاربران تازه بات شما را پیدا می‌کنند", href: "/showcase" },
    ] },
    { group: "سایت‌ها", items: [
      { title: "سایت محلی با آدرس ثابت", text: "سایت روی کامپیوتر شما، با زیردامنه‌ی دالان", soon: true },
      { title: "دامنه‌ی خودتان", text: "سایت با دامنه‌ی شخصی", soon: true },
      { title: "بررسی و تکرار وب‌هوک", text: "درخواست‌ها را ببینید و دوباره بفرستید", soon: true },
    ] },
  ],
  en: [
    { group: "Build and test", items: [
      { title: "Run a bot from home", text: "Your own computer, no server, no public IP", href: "#how" },
      { title: "Telegram bots, no code change", text: "Change only the server address", href: "#try" },
      { title: "AI assistants", text: "Make and manage bots with the CLI and MCP", href: "/llms.txt" },
    ] },
    { group: "Reach users", items: [
      { title: "Real users", text: "Every user is verified by email or SMS", href: "#features" },
      { title: "When international internet is cut", text: "Dalan runs inside Iran", href: "#features" },
      { title: "Bot showcase", text: "New users find your bot", href: "/showcase" },
    ] },
    { group: "Sites", items: [
      { title: "A local site with a fixed address", text: "A site on your computer, on a Dalan subdomain", soon: true },
      { title: "Your own domain", text: "Your site on your own domain", soon: true },
      { title: "Inspect and replay webhooks", text: "See requests and send them again", soon: true },
    ] },
  ],
};

/** `branches`: the traffic splits in two below Dalan; `frame` names the place both branches run in. */
export interface Product {
  label: string; title: string; text: string; soon: boolean; from: string; guards: string[];
  branches: { via: string; to: string }[]; frame: string;
}

/** CLI-26: the product cards and the words of their traffic pictures. */
export const products: Record<Lang, Product[]> = {
  fa: [
    { label: "رله‌ی بات", title: "یک دالان بین کاربران و بات شما.", text: "کاربر در دالان پیام می‌دهد. بات روی کامپیوتر شما پیام را با getUpdates برمی‌دارد.", soon: false,
      from: "کاربران تأییدشده", branches: [{ via: "getUpdates", to: "@shop_bot" }, { via: "getUpdates", to: "@game_bot" }], frame: "کامپیوتر شما · فقط اتصال خروجی", guards: ["محدودیت سرعت", "گواه کار پیش از پیامک", "ثبت دسترسی"] },
    { label: "سایت‌ها", title: "سایت روی کامپیوترتان، با یک آدرس ثابت.", text: "یک برنامه‌ی کوچک روی کامپیوتر شما به دالان وصل می‌شود و سایت محلی را به بازدیدکننده‌ها نشان می‌دهد.", soon: true,
      from: "بازدیدکننده‌ها", branches: [{ via: "/", to: "localhost:3000" }, { via: "/api", to: "localhost:8080" }], frame: "کامپیوتر شما", guards: ["رمز ورود", "فهرست IP مجاز", "بازبینی درخواست‌ها"] },
  ],
  en: [
    { label: "Bot relay", title: "A passage between users and your bot.", text: "Users write in Dalan. The bot on your computer picks up messages with getUpdates.", soon: false,
      from: "Verified users", branches: [{ via: "getUpdates", to: "@shop_bot" }, { via: "getUpdates", to: "@game_bot" }], frame: "Your computer · outgoing only", guards: ["Rate limits", "Proof of work before SMS", "Access log"] },
    { label: "Sites", title: "A site on your computer, at a fixed address.", text: "A small program on your computer connects out to Dalan and shows your local site to visitors.", soon: true,
      from: "Visitors", branches: [{ via: "/", to: "localhost:3000" }, { via: "/api", to: "localhost:8080" }], frame: "Your computer", guards: ["Basic Auth", "IP allowlist", "Request inspector"] },
  ],
};

export interface OfferGroup { title: string; text: string; items: { text: string; soon?: boolean }[] }

/** CLI-37: what you get. The first three groups are built today; every item of the last one is Layer 2 and says "soon". */
export const offers: Record<Lang, OfferGroup[]> = {
  fa: [
    { title: "رله‌ی بات", text: "پیام‌ها بین کاربران و بات روی کامپیوتر شما.", items: [
      { text: "Bot API سازگار با تلگرام: فقط آدرس سرور عوض می‌شود" },
      { text: "دریافت پیام با getUpdates؛ کامپیوتر شما فقط اتصال خروجی دارد" },
      { text: "دکمه، منوی دستورها، فایل، پیام سنجاق‌شده و ویرایش‌شده" },
      { text: "وضعیت روشن بودن بات و نشان «نرسید»" },
      { text: "اعلان (Web Push) برای کاربران" },
      { text: "ویترین، تا کاربران تازه بات شما را پیدا کنند" },
    ] },
    { title: "کاربران و امنیت", text: "کاربر واقعی، بدون این‌که خودتان ورود بسازید.", items: [
      { text: "کاربران تأییدشده با گیت‌هاب، ایمیل یا کد پیامک" },
      { text: "بات هرگز شماره موبایل کاربر را نمی‌بیند" },
      { text: "محدودیت سرعت روی هر در و گواه کار پیش از هر پیامک" },
      { text: "توکن بات و کلید دسترسی به شکل hash" },
      { text: "ثبت IP، پورت و زمان هر اتصال، طبق قانون" },
    ] },
    { title: "ابزار سازنده", text: "بات بسازید و مدیریت کنید، با دست یا با هوش مصنوعی.", items: [
      { text: "ابزار خط فرمان dalan.mjs" },
      { text: "سرور MCP برای دستیارهای هوش مصنوعی" },
      { text: "کلید دسترسی که هر وقت بخواهید پاک می‌شود" },
      { text: "صفحه‌ی llms.txt که دالان را به هوش مصنوعی معرفی می‌کند" },
    ] },
    { title: "سایت‌ها و درگاه", text: "سایت یا API روی کامپیوتر شما، با یک آدرس عمومی.", items: [
      { text: "سایت محلی با زیردامنه‌ی ثابت دالان", soon: true },
      { text: "دامنه‌ی خودتان", soon: true },
      { text: "لینک پیش‌نمایش برای نشان دادن کار", soon: true },
      { text: "دیدن و دوباره فرستادن وب‌هوک‌ها", soon: true },
      { text: "بررسی امضای وب‌هوک پیش از رسیدن به شما", soon: true },
      { text: "رمز ورود و فهرست IP مجاز جلوی سایت", soon: true },
      { text: "کلید و محدودیت سرعت جلوی API شما", soon: true },
      { text: "سرور MCP محلی شما، در دسترس دستیارهای هوش مصنوعی", soon: true },
    ] },
  ],
  en: [
    { title: "Bot relay", text: "Messages between users and the bot on your computer.", items: [
      { text: "Telegram-compatible Bot API: change only the server address" },
      { text: "Messages with getUpdates; your computer only calls out" },
      { text: "Buttons, command menu, files, pinned and edited messages" },
      { text: "Bot online status and a \"not delivered\" mark" },
      { text: "Notifications for users (Web Push)" },
      { text: "Showcase, so new users find your bot" },
    ] },
    { title: "Users and safety", text: "Real users, without building login yourself.", items: [
      { text: "Users verified by GitHub, email or SMS code" },
      { text: "Bots never see a user's mobile number" },
      { text: "Rate limits on every door, proof of work before each SMS" },
      { text: "Bot tokens and access keys stored hashed" },
      { text: "IP, port and time of every connection logged, as the law asks" },
    ] },
    { title: "Builder tools", text: "Make and manage bots by hand or with AI.", items: [
      { text: "Command line tool dalan.mjs" },
      { text: "MCP server for AI assistants" },
      { text: "Access keys you can delete any time" },
      { text: "An llms.txt page that explains Dalan to AI" },
    ] },
    { title: "Sites and gateway", text: "A site or API on your computer, at a public address.", items: [
      { text: "A local site at a fixed Dalan subdomain", soon: true },
      { text: "Your own domain", soon: true },
      { text: "Preview links to show your work", soon: true },
      { text: "See webhooks and send them again", soon: true },
      { text: "Webhook signatures checked before they reach you", soon: true },
      { text: "Password and IP allowlist in front of a site", soon: true },
      { text: "Keys and rate limits in front of your API", soon: true },
      { text: "Your local MCP server, reachable by AI assistants", soon: true },
    ] },
  ],
};

/** CLI-37: what Dalan does not offer, said plainly. */
export const notOffered: Record<Lang, string> = {
  fa: "چیزهایی که نداریم: پورت TCP خام، SSH یا دسکتاپ از راه دور، و درگاه هوش مصنوعی.",
  en: "What we do not offer: raw TCP ports, SSH or remote desktop, and an AI gateway.",
};

/** CLI-26: "Try it right now". Real commands only: get the CLI (CLI-31) from the site and make a bot. */
export function installCommands(numbers: PublicNumbers): { os: string; code: string }[] {
  const site = numbers.site_url;
  const unix = `curl -fsSO ${site}/dalan.mjs\nexport DALAN_URL=${site} DALAN_KEY=dalan_...\nnode dalan.mjs create my_bot "My bot"`;
  return [
    { os: "Linux", code: unix },
    { os: "macOS", code: unix },
    { os: "Windows", code: `iwr ${site}/dalan.mjs -OutFile dalan.mjs\n$env:DALAN_URL="${site}"; $env:DALAN_KEY="dalan_..."\nnode dalan.mjs create my_bot "My bot"` },
    { os: "Docker", code: `docker run --rm -e DALAN_URL=${site} -e DALAN_KEY=dalan_... node:lts-alpine \\\n  sh -c 'wget -q ${site}/dalan.mjs && node dalan.mjs create my_bot "My bot"'` },
  ];
}

/** CLI-26: what Dalan really does for safety (CLAUDE.md rules 5, 7, 8 and 13). */
export const security: Record<Lang, string[]> = {
  fa: ["کاربران تأییدشده با ایمیل یا پیامک", "بات هرگز شماره نمی‌بیند", "توکن بات به شکل hash", "رمز با hash کند", "محدودیت سرعت روی هر در",
    "گواه کار پیش از هر پیامک", "گزارش کارهای ادمین", "سرور در ایران", "بدون اسکریپت و فونت خارجی", "فقط HTTPS"],
  en: ["Users verified by email or SMS", "Bots never see phone numbers", "Hashed bot tokens", "Slow password hashing", "Rate limits on every door",
    "Proof of work before each SMS", "Admin audit log", "Servers in Iran", "No foreign scripts or fonts", "HTTPS only"],
};

export interface Step { title: string; text: string; code?: string }

/** CLI-26: three steps. The addresses come from the public settings (rule 4), never from this file. */
export function steps(lang: Lang, numbers: PublicNumbers): Step[] {
  const api = `${numbers.bot_api_url}/bot<token>/getUpdates`;
  const link = `${numbers.site_url}/@your_bot`;
  return lang === "fa" ? [
    { title: "توکن بگیرید", text: "وارد شوید، در بخش حساب درخواست سازنده شدن بدهید، بات بسازید و توکنش را بردارید." },
    { title: "یک خط را عوض کنید", text: "در کد بات، به جای api.telegram.org آدرس دالان را بگذارید. بقیه‌ی کد همان است.", code: api },
    { title: "روی کامپیوتر خودتان اجرا کنید", text: "بات را روشن کنید. کاربران با این لینک به آن می‌رسند:", code: link },
  ] : [
    { title: "Get a token", text: "Log in, ask to become a builder in your account screen, create a bot and copy its token." },
    { title: "Change one line", text: "In your bot code, put Dalan's address in place of api.telegram.org. The rest of the code stays the same.", code: api },
    { title: "Run it on your own computer", text: "Start the bot. Users reach it with this link:", code: link },
  ];
}

/** CLI-26: the closing call of the landing page. */
export const closing: Record<Lang, { title: string; glow: string; text: string }> = {
  fa: { title: "نوبت شماست.", glow: "بات‌تان را روشن کنید.", text: "وارد شوید و اولین بات‌تان را بسازید، یا بات‌های ویترین را ببینید." },
  en: { title: "Your turn.", glow: "Put your bot online.", text: "Log in and make your first bot, or look at the bots in the showcase." },
};

/** CLI-26: the small labels above the landing sections and the words of the footer. */
export const labels: Record<Lang, Record<"products" | "offers" | "offersTitle" | "try" | "tryTitle" | "tryNote" | "security" | "problems" | "soon" | "resources" | "product" | "start", string>> = {
  fa: { products: "محصولات ما", offers: "چه چیزی می‌گیرید", offersTitle: "هر چه بین کاربران و کامپیوتر شما لازم است.", try: "همین الان", tryTitle: "همین الان یک بات بسازید.", tryNote: "کد بات شما با هر زبانی کار می‌کند: هر کتابخانه‌ی بات تلگرام، فقط با عوض کردن آدرس سرور.",
    security: "امنیت از روز اول.", problems: "چه مشکلی را حل می‌کنیم", soon: "به‌زودی", resources: "منابع", product: "محصول", start: "آزمایش کنید (رایگان)" },
  en: { products: "Our products", offers: "What you get", offersTitle: "Everything between your users and your computer.", try: "Right now", tryTitle: "Make a bot right now.", tryNote: "Your bot code works in any language: any Telegram bot library, after changing only the server address.",
    security: "Security from day one.", problems: "Problems we solve", soon: "Soon", resources: "Resources", product: "Product", start: "Try it (free)" },
};

/** CLI-27: the six points of DESIGN section 13. `n` writes a number in the page's digits. */
export function privacyPoints(lang: Lang, numbers: PublicNumbers, n: (value: number) => string): string[] {
  const m = n(numbers.history_messages), d = n(numbers.history_days), f = n(numbers.file_days), a = n(numbers.access_log_days);
  return lang === "fa" ? [
    "یک بات فقط وقتی در ویترین دیده می‌شود که سازنده‌اش بخواهد و ادمین تأیید کند. هر بات در اول خصوصی است.",
    "بات شناسه و نام شما را در دالان می‌بیند، ولی هرگز شماره موبایل شما را نمی‌بیند.",
    "پیام‌ها بدون رمزگذاری سرتاسری از سرور دالان می‌گذرند. یعنی دالان از نظر فنی می‌تواند آن‌ها را بخواند.",
    `از هر گفتگو فقط ${m} پیام آخر، و نه قدیمی‌تر از ${d} روز، نگه داشته می‌شود. فایل‌ها بعد از ${f} روز پاک می‌شوند.`,
    "کامپیوتری که بات روی آن کار می‌کند مال سازنده‌ی بات است. دالان فقط پیام‌ها را بین دو طرف می‌رساند و مسئول روشن بودن آن کامپیوتر نیست.",
    `دالان آدرس IP، پورت و زمان اتصال کاربران و بات‌ها را ${a} روز نگه می‌دارد تا بتواند به شکایت‌های قانونی پاسخ دهد.`,
  ] : [
    "A bot is in the showcase only if its builder asks and the admin approves. Every bot starts private.",
    "A bot sees your Dalan id and name, but never your mobile number.",
    "Messages pass through Dalan's server without end-to-end encryption. This means Dalan can technically read them.",
    `For each chat we keep only the last ${m} messages, and none older than ${d} days. Files are deleted after ${f} days.`,
    "The computer that runs a bot belongs to the bot's builder. Dalan only carries messages between the two sides and is not responsible for that computer being on.",
    `Dalan keeps the IP address, port and time of connection of users and bots for ${a} days, to answer legal complaints.`,
  ];
}

/** CLI-27: the same six points as questions. */
export function faqItems(lang: Lang, numbers: PublicNumbers, n: (value: number) => string): { question: string; answer: string }[] {
  const answers = privacyPoints(lang, numbers, n);
  const e = n(numbers.update_expiry_seconds);
  answers[4] += lang === "fa"
    ? ` بالای گفتگو می‌بینید بات آخرین بار کی روشن بوده است. پیامی که بات در ${e} ثانیه نخواند، «نرسید» نشان داده می‌شود.`
    : ` The chat shows when the bot was last online. A message the bot does not read within ${e} seconds is marked "not delivered".`;
  const questions = lang === "fa" ? [
    "آیا بات من خودبه‌خود به همه نشان داده می‌شود؟",
    "آیا بات شماره موبایل مرا می‌بیند؟",
    "آیا دالان پیام‌های مرا می‌خواند؟",
    "پیام‌هایم تا کی نگه داشته می‌شود؟",
    "اگر بات جواب ندهد چه می‌شود؟",
    "چرا دالان آدرس IP مرا نگه می‌دارد؟",
  ] : [
    "Is my bot shown to everyone by itself?",
    "Can a bot see my mobile number?",
    "Does Dalan read my messages?",
    "How long are my messages kept?",
    "What if the bot does not answer?",
    "Why does Dalan keep my IP address?",
  ];
  return questions.map((question, i) => ({ question, answer: answers[i]! }));
}

const categoryNames: Record<Lang, Record<string, string>> = {
  fa: { games: "بازی", tools: "ابزار", education: "آموزش", news: "خبر", shopping: "خرید", other: "دیگر" },
  en: { games: "Games", tools: "Tools", education: "Education", news: "News", shopping: "Shopping", other: "Other" },
};

/** CLI-28: the start categories have names; a key the admin added is shown as it is. */
export function categoryName(lang: Lang, key: string): string {
  return categoryNames[lang][key] ?? key;
}

export type ShowcaseState = "pending" | "listed" | "refused";

export interface ShowcaseEntry {
  state: ShowcaseState;
  description: string;
  category: string;
  icon: boolean;
}

/** REL-43: a bot of the public showcase. */
export interface ShowcaseBot {
  id: number;
  username: string;
  name: string;
  description: string;
  category: string;
  icon: boolean;
}

const stateNames: Record<Lang, Record<ShowcaseState | "none", string>> = {
  fa: { none: "در ویترین نیست", pending: "در انتظار بررسی", listed: "در ویترین", refused: "رد شد" },
  en: { none: "Not listed", pending: "Waiting for review", listed: "Listed", refused: "Refused" },
};

/** CLI-29: the state of a bot's entry, or of no entry. */
export function showcaseStateName(lang: Lang, entry: ShowcaseEntry | null): string {
  return stateNames[lang][entry?.state ?? "none"];
}

/** CLI-28: the first letter of the name, for a bot with no icon. */
export function initial(name: string): string {
  return [...name.trim()][0]?.toUpperCase() ?? "?";
}

/**
 * CLI-30: text a builder pastes into an AI agent. It names the addresses from the server (ADM-15) and never a key:
 * the builder gives the key to the agent's settings, not to the chat.
 */
export function readyPrompt(lang: Lang, site: string, botApi: string): string {
  return lang === "fa" ? `پروژهٔ من را به دالان وصل کن. دالان بات یا وب‌اپ را از کامپیوتر خانه، بدون IP عمومی، اجرا می‌کند.
اول این صفحه را بخوان: ${site}/llms.txt
- اگر بات تلگرامی است، فقط نشانی سرور را ${botApi} بگذار و توکن را توکن دالان کن.
- برای ساختن بات و توکن، از سرور MCP دالان استفاده کن: ${site}/api/mcp (کلید دسترسی در متغیر DALAN_KEY است).
- یا از ابزار خط فرمان: ${site}/dalan.mjs
کلید یا توکن را هرگز در مخزن کد نگذار.` : `Connect my project to Dalan. Dalan runs a bot or web app from a home computer with no public IP.
First read: ${site}/llms.txt
- For a Telegram bot, change only the server address to ${botApi} and use the Dalan token.
- To create the bot and its token, use Dalan's MCP server: ${site}/api/mcp (the access key is in DALAN_KEY).
- Or the command line tool: ${site}/dalan.mjs
Never put the key or a token in the repository.`;
}
