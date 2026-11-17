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
}

/** CLI-26: which screen an address shows. Unknown addresses never reach the page: the server answers 404. */
export type Page = "landing" | "privacy" | "faq" | "showcase" | "login" | "bots" | "chat";

export function pageFor(path: string, loggedIn: boolean): Page {
  if (path === "/privacy" || path === "/faq" || path === "/showcase") return path.slice(1) as Page;
  if (path.startsWith("/@")) return loggedIn ? "chat" : "login";
  return loggedIn ? "bots" : "landing";
}

/** CLI-26: the three messages of DESIGN section 13, in this order. Never "free tunnel". */
export const landing: Record<Lang, { title: string; text: string }[]> = {
  fa: [
    { title: "برنامه‌تان را از کامپیوتر خودتان اجرا کنید.", text: "سرور نخرید. بات یا برنامه روی کامپیوتر خانه یا محل کارتان می‌ماند و فقط به دالان وصل می‌شود. IP عمومی لازم نیست." },
    { title: "کاربر واقعی، با شماره موبایل تأییدشده.", text: "هر کاربر دالان یک بار با پیامک تأیید شده است. لازم نیست خودتان ورود پیامکی بسازید." },
    { title: "وقتی اینترنت بین‌الملل قطع است، کار می‌کند.", text: "دالان در ایران است. بات شما و کاربرانش بدون اینترنت بین‌الملل هم به هم می‌رسند." },
  ],
  en: [
    { title: "Run your app from your own computer.", text: "Do not buy a server. Your bot or app stays on your home or office computer and only connects out to Dalan. No public IP is needed." },
    { title: "Get real users, verified by mobile number.", text: "Every Dalan user is verified once by SMS. You do not need to build SMS login." },
    { title: "Keep working when international internet is cut.", text: "Dalan runs inside Iran. Your bot and its users reach each other without international internet." },
  ],
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
