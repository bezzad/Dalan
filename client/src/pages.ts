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
export type Page = "landing" | "privacy" | "faq" | "showcase" | "guide" | "guideBot" | "guideSite" | "login"
  | "chats" | "bots" | "botNew" | "botPage" | "account" | "admin" | "apps" | "chat" | "notFound";

/** CLI-51: the screens of a logged-in user, each at its own address. */
const appPages: Record<string, Page> = { "/chats": "chats", "/bots": "bots", "/bots/new": "botNew", "/account": "account", "/admin": "admin", "/apps": "apps" };

/** CLI-49: the username of a bot's own page (`/bots/<username>`), or null. */
export function botPageUsername(path: string): string | null {
  // A bot username starts with a letter (REL-36), so "/bots/7" is no bot page.
  const name = /^\/bots\/([A-Za-z][A-Za-z0-9_]*)$/.exec(path)?.[1];
  return name && name !== "new" ? name : null;
}

export function pageFor(path: string, loggedIn: boolean): Page {
  if (path === "/privacy" || path === "/faq" || path === "/showcase" || path === "/guide") return path.slice(1) as Page;
  // CLI-38: the guide of each plan.
  if (path === "/guide/bot") return "guideBot";
  if (path === "/guide/site") return "guideSite";
  if (path.startsWith("/@")) return loggedIn ? "chat" : "login";
  const screen = appPages[path] ?? (botPageUsername(path) ? "botPage" : null);
  if (screen) return loggedIn ? screen : "login";
  // CLI-40: an address we do not know is a closed gate, not the landing page.
  return path === "/" || path === "" ? "landing" : "notFound";
}

/** CLI-26: the three messages of DESIGN section 13, in this order. Never "free tunnel". */
export const landing: Record<Lang, { title: string; text: string }[]> = {
  fa: [
    { title: "برنامه‌ی خود را روی کامپیوتر خودتان اجرا کنید", text: "نیازی به خرید سرور نیست. بات یا برنامه روی کامپیوتر خانه یا محل کارتان اجرا می‌شود و فقط به دالان متصل می‌شود. IP عمومی هم لازم نیست." },
    { title: "کاربران واقعی، تأییدشده با ایمیل یا پیامک", text: "هویت همه‌ی کاربران دالان با ایمیل یا پیامک تأیید شده است؛ نیازی نیست خودتان سیستم ورود بسازید." },
    { title: "حتی در زمان قطعی اینترنت بین‌الملل کار می‌کند", text: "سرورهای دالان در ایران است؛ بات شما و کاربرانش بدون اینترنت بین‌الملل هم به هم متصل می‌شوند." },
  ],
  en: [
    { title: "Run your app from your own computer", text: "Do not buy a server. Your bot or app stays on your home or office computer and only connects out to Dalan. No public IP is needed." },
    { title: "Get real users, verified by email or SMS", text: "Every Dalan user is verified by email or SMS. You do not need to build a login." },
    { title: "Keep working when outside links are down", text: "Dalan's servers are inside the country. Your bot and its users still reach each other when links to the outside world are down." },
  ],
};

/** CLI-26: the hero at the top of the landing page. `glow` is drawn in the accent green on its own line. */
export const hero: Record<Lang, { title: string; glow: string; text: string; plans: string }> = {
  fa: {
    title: "بات روی کامپیوتر خودتان؛",
    glow: "در دسترس کاربران سراسر ایران",
    text: "دالان کاربران تأییدشده را به بات یا برنامه‌ای که روی کامپیوتر شما اجرا می‌شود متصل می‌کند؛ بدون نیاز به سرور، IP عمومی یا باز کردن پورت.",
    plans: "طرح‌ها را ببینید",
  },
  en: {
    title: "Your bot on your computer,",
    glow: "reachable from anywhere",
    text: "Dalan brings verified users to the bot or app on your own computer. No server, no public IP, no open port.",
    plans: "See the plans",
  },
};

/** `branches`: the traffic splits in two below Dalan; `frame` names the place both branches run in. */
export interface Product {
  label: string; title: string; text: string; soon: boolean; from: string; guards: string[];
  /** CLI-26: "Get started" opens the guide of this plan (CLI-38). */
  guide: "/guide/bot" | "/guide/site";
  branches: { via: string; to: string }[]; frame: string;
}

/** CLI-26: the plans: the product cards and the words of their traffic pictures. Both are built (CLI-39). */
export const products: Record<Lang, Product[]> = {
  fa: [
    { label: "اتصال بات", title: "مسیری امن میان کاربران و بات شما", text: "کاربر در دالان پیام می‌فرستد و بات روی کامپیوتر شما پیام‌ها را با getUpdates دریافت می‌کند.", soon: false, guide: "/guide/bot",
      from: "کاربران تأییدشده", branches: [{ via: "getUpdates", to: "@shop_bot" }, { via: "getUpdates", to: "@game_bot" }], frame: "کامپیوتر شما · فقط اتصال خروجی", guards: ["محدودیت تعداد درخواست", "بررسی امنیتی پیش از ارسال پیامک", "ثبت گزارش دسترسی‌ها"] },
    { label: "سایت‌ها", title: "سایت روی کامپیوتر شما، با یک آدرس ثابت", text: "یک برنامه‌ی کوچک روی کامپیوتر شما به دالان متصل می‌شود و سایت محلی‌تان را در اختیار بازدیدکنندگان قرار می‌دهد.", soon: false, guide: "/guide/site",
      from: "بازدیدکنندگان", branches: [{ via: "/", to: "localhost:3000" }, { via: "/api", to: "localhost:8080" }], frame: "کامپیوتر شما", guards: ["HTTPS", "زیردامنه‌ی ثابت", "ثبت گزارش دسترسی‌ها"] },
  ],
  en: [
    { label: "Bot relay", title: "A passage between users and your bot", text: "Users write in Dalan. The bot on your computer picks up messages with getUpdates.", soon: false, guide: "/guide/bot",
      from: "Verified users", branches: [{ via: "getUpdates", to: "@shop_bot" }, { via: "getUpdates", to: "@game_bot" }], frame: "Your computer · outgoing only", guards: ["Rate limits", "Proof of work before SMS", "Access log"] },
    { label: "Sites", title: "A site on your computer, at a fixed address", text: "A small program on your computer connects out to Dalan and shows your local site to visitors.", soon: false, guide: "/guide/site",
      from: "Visitors", branches: [{ via: "/", to: "localhost:3000" }, { via: "/api", to: "localhost:8080" }], frame: "Your computer", guards: ["HTTPS", "Fixed subdomain", "Access log"] },
  ],
};

export interface OfferGroup { title: string; text: string; items: { text: string; soon?: boolean }[] }

/** CLI-37: what you get. The first three groups are built today; in the last one only the local site is built (CLI-39), the rest says "soon". */
export const offers: Record<Lang, OfferGroup[]> = {
  fa: [
    { title: "پیام‌رسانی بات", text: "انتقال پیام میان کاربران و باتی که روی کامپیوتر شما اجرا می‌شود.", items: [
      { text: "Bot API سازگار با تلگرام؛ کافی است آدرس سرور را تغییر دهید" },
      { text: "دریافت پیام با getUpdates؛ کامپیوتر شما فقط اتصال خروجی برقرار می‌کند" },
      { text: "پشتیبانی از دکمه، منوی دستورات، فایل، پیام سنجاق‌شده و ویرایش پیام" },
      { text: "نمایش وضعیت آنلاین بودن بات و علامت «تحویل نشد»" },
      { text: "اعلان (Web Push) برای کاربران" },
      { text: "ویترین بات‌ها، تا کاربران جدید بات شما را پیدا کنند" },
    ] },
    { title: "کاربران و امنیت", text: "کاربران واقعی، بدون نیاز به ساخت سیستم ورود.", items: [
      { text: "کاربران تأییدشده با گیت‌هاب، ایمیل یا کد پیامکی" },
      { text: "بات هرگز شماره‌ی موبایل کاربر را نمی‌بیند" },
      { text: "محدودیت تعداد درخواست در همه‌ی بخش‌ها و بررسی امنیتی پیش از هر پیامک" },
      { text: "توکن بات و کلید دسترسی به‌صورت هش‌شده (hash) نگهداری می‌شوند" },
      { text: "ثبت IP، پورت و زمان هر اتصال، مطابق قانون" },
    ] },
    { title: "ابزارهای توسعه‌دهنده", text: "بات بسازید و مدیریت کنید؛ به‌صورت دستی یا با کمک هوش مصنوعی.", items: [
      { text: "ابزار خط فرمان dalan.mjs" },
      { text: "سرور MCP برای دستیارهای هوش مصنوعی" },
      { text: "کلید دسترسی که هر زمان بخواهید قابل حذف است" },
      { text: "فایل llms.txt برای معرفی دالان به هوش مصنوعی" },
    ] },
    { title: "سایت‌ها و API", text: "سایت یا API روی کامپیوتر شما، با یک آدرس عمومی.", items: [
      { text: "سایت محلی با زیردامنه‌ی ثابت دالان" },
      { text: "دامنه‌ی اختصاصی شما", soon: true },
      { text: "لینک پیش‌نمایش برای نمایش کار به دیگران", soon: true },
      { text: "مشاهده و ارسال دوباره‌ی وب‌هوک‌ها", soon: true },
      { text: "بررسی امضای وب‌هوک پیش از رسیدن به شما", soon: true },
      { text: "محافظت از سایت با رمز عبور و فهرست IPهای مجاز", soon: true },
      { text: "محافظت از API با کلید دسترسی و محدودیت تعداد درخواست", soon: true },
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
      { text: "A local site at a fixed Dalan subdomain" },
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
  fa: "امکاناتی که فعلاً ارائه نمی‌دهیم: پورت TCP خام، SSH یا دسکتاپ از راه دور، و درگاه هوش مصنوعی.",
  en: "What we do not offer: raw TCP ports, SSH or remote desktop, and an AI gateway.",
};

/** CLI-26: what Dalan really does for safety (CLAUDE.md rules 5, 7, 8 and 13). */
export const security: Record<Lang, string[]> = {
  fa: ["کاربران تأییدشده با ایمیل یا پیامک", "بات هرگز شماره‌ی کاربر را نمی‌بیند", "توکن بات به‌صورت هش‌شده", "رمز عبور با الگوریتم هش امن",
    "محدودیت تعداد درخواست در همه‌ی بخش‌ها", "بررسی امنیتی پیش از هر پیامک", "ثبت گزارش فعالیت‌های مدیر", "سرور در ایران", "بدون اسکریپت و فونت خارجی", "فقط HTTPS"],
  en: ["Users verified by email or SMS", "Bots never see phone numbers", "Hashed bot tokens", "Slow password hashing", "Rate limits on every door",
    "Proof of work before each SMS", "Admin audit log", "Servers inside the country", "No foreign scripts or fonts", "HTTPS only"],
};

/** CLI-26: the closing call of the landing page. */
export const closing: Record<Lang, { title: string; glow: string; text: string }> = {
  fa: { title: "همین حالا شروع کنید", glow: "بات خود را راه‌اندازی کنید", text: "وارد شوید و اولین بات خود را بسازید، یا بات‌های ویترین را ببینید." },
  en: { title: "Your turn", glow: "Put your bot online", text: "Log in and make your first bot, or look at the bots in the showcase." },
};

/** CLI-26: the small labels above the landing sections and the words of the footer. */
export const labels: Record<Lang, Record<"plans" | "plansTitle" | "getStarted" | "offers" | "offersTitle" | "why" | "security" | "soon" | "resources" | "product" | "start", string>> = {
  fa: { plans: "طرح‌ها", plansTitle: "چه چیزی را می‌خواهید آنلاین کنید؟", getStarted: "شروع کنید", offers: "امکانات", offersTitle: "هر آنچه برای ارتباط کاربران با کامپیوتر شما لازم است",
    why: "چرا دالان؟", security: "امنیت، از همان ابتدا", soon: "به‌زودی", resources: "منابع", product: "محصولات", start: "رایگان امتحان کنید" },
  en: { plans: "Plans", plansTitle: "What do you want to put online?", getStarted: "Get started", offers: "What you get", offersTitle: "Everything between your users and your computer",
    why: "Why Dalan", security: "Security from day one", soon: "Soon", resources: "Resources", product: "Product", start: "Try it (free)" },
};

/** CLI-27: the six points of DESIGN section 13. `n` writes a number in the page's digits. */
export function privacyPoints(lang: Lang, numbers: PublicNumbers, n: (value: number) => string): string[] {
  const m = n(numbers.history_messages), d = n(numbers.history_days), f = n(numbers.file_days), a = n(numbers.access_log_days);
  return lang === "fa" ? [
    "یک بات فقط زمانی در ویترین نمایش داده می‌شود که توسعه‌دهنده‌اش درخواست دهد و مدیر دالان آن را تأیید کند. هر بات در ابتدا خصوصی است.",
    "بات شناسه و نام شما در دالان را می‌بیند، اما هرگز شماره‌ی موبایل شما را نمی‌بیند.",
    "پیام‌ها بدون رمزنگاری سرتاسری از سرور دالان عبور می‌کنند؛ بنابراین دالان از نظر فنی می‌تواند آن‌ها را بخواند.",
    `از هر گفت‌وگو فقط ${m} پیام آخر، و نه قدیمی‌تر از ${d} روز، نگهداری می‌شود. فایل‌ها پس از ${f} روز حذف می‌شوند.`,
    "کامپیوتری که بات روی آن اجرا می‌شود متعلق به سازنده‌ی بات است. دالان فقط پیام‌ها را میان دو طرف منتقل می‌کند و مسئولیتی در قبال روشن بودن آن کامپیوتر ندارد.",
    `دالان آدرس IP، پورت و زمان اتصال کاربران و بات‌ها را ${a} روز نگهداری می‌کند تا بتواند به شکایت‌های قانونی پاسخ دهد.`,
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
    ? ` در بالای گفت‌وگو می‌بینید بات آخرین بار چه زمانی آنلاین بوده است. پیامی که بات در ${e} ثانیه دریافت نکند، با علامت «تحویل نشد» مشخص می‌شود.`
    : ` The chat shows when the bot was last online. A message the bot does not read within ${e} seconds is marked "not delivered".`;
  const questions = lang === "fa" ? [
    "آیا بات من به‌طور خودکار به همه نمایش داده می‌شود؟",
    "آیا بات شماره‌ی موبایل مرا می‌بیند؟",
    "آیا دالان پیام‌های مرا می‌خواند؟",
    "پیام‌هایم تا چه مدت نگهداری می‌شوند؟",
    "اگر بات پاسخ ندهد چه می‌شود؟",
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
  fa: { games: "بازی", tools: "ابزار", education: "آموزش", news: "خبر", shopping: "خرید", other: "سایر" },
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
  /** REL-51 */
  icon_version: number;
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
  return lang === "fa" ? `پروژه‌ی من را به دالان متصل کن. دالان بات یا برنامه‌ی تحت وب را از کامپیوتر خانه، بدون IP عمومی، اجرا می‌کند.
اول این صفحه را بخوان: ${site}/llms.txt
- اگر بات تلگرام است، فقط آدرس سرور را ${botApi} قرار بده و توکن را با توکن دالان جایگزین کن.
- برای ساختن بات و توکن، از سرور MCP دالان استفاده کن: ${site}/api/mcp (کلید دسترسی در متغیر DALAN_KEY است).
- یا از ابزار خط فرمان: ${site}/dalan.mjs
کلید یا توکن را هرگز در مخزن کد نگذار.` : `Connect my project to Dalan. Dalan runs a bot or web app from a home computer with no public IP.
First read: ${site}/llms.txt
- For a Telegram bot, change only the server address to ${botApi} and use the Dalan token.
- To create the bot and its token, use Dalan's MCP server: ${site}/api/mcp (the access key is in DALAN_KEY).
- Or the command line tool: ${site}/dalan.mjs
Never put the key or a token in the repository.`;
}
