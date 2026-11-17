// CLI-04: Persian (right to left) first, English second. Every text is in both lists.

export const texts = {
  fa: {
    appName: "دالان",
    loginTitle: "ورود آزمایشی",
    loginNote: "این ورود فقط برای توسعه است و پیامک نمی‌فرستد.",
    loginOff: "ورود هنوز روی این سرور فعال نیست.",
    phone: "شماره موبایل",
    name: "نام",
    login: "ورود",
    logout: "خروج",
    language: "English",
    myBots: "بات‌های من",
    noBots: "هنوز با هیچ باتی گفتگو نکرده‌اید. نام کاربری بات را وارد کنید.",
    botUsername: "نام کاربری بات",
    open: "باز کردن",
    back: "بازگشت",
    start: "شروع",
    send: "ارسال",
    message: "پیام",
    older: "پیام‌های قدیمی‌تر",
    connecting: "در حال اتصال…",
    botNotFound: "باتی با این نام پیدا نشد.",
    badPhone: "شماره موبایل و نام را درست وارد کنید. مثال: 09121234567",
    textTooLong: "متن پیام خیلی بلند است.",
    tooFast: "کمی صبر کنید و دوباره امتحان کنید.",
    failed: "انجام نشد. اتصال را بررسی کنید و دوباره امتحان کنید.",
  },
  en: {
    appName: "Dalan",
    loginTitle: "Development login",
    loginNote: "This login is for development only and sends no SMS.",
    loginOff: "Login is not enabled on this server yet.",
    phone: "Mobile number",
    name: "Name",
    login: "Log in",
    logout: "Log out",
    language: "فارسی",
    myBots: "My bots",
    noBots: "You have no chats yet. Enter a bot's username.",
    botUsername: "Bot username",
    open: "Open",
    back: "Back",
    start: "Start",
    send: "Send",
    message: "Message",
    older: "Older messages",
    connecting: "Connecting…",
    botNotFound: "No bot with this name was found.",
    badPhone: "Enter a valid mobile number and a name. Example: 09121234567",
    textTooLong: "The message is too long.",
    tooFast: "Wait a moment and try again.",
    failed: "That did not work. Check your connection and try again.",
  },
} as const;

export type Lang = keyof typeof texts;
export type TextKey = keyof (typeof texts)["fa"];

const StorageKey = "dalan.lang";

/** The two things of the page that follow the language. `document.documentElement` fits. */
export interface PageRoot {
  lang: string;
  dir: string;
}

/** The part of `localStorage` that is used. */
export interface LangStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
}

export class Language {
  private readonly root: PageRoot;
  private readonly storage: LangStorage;
  current: Lang;

  constructor(root: PageRoot, storage: LangStorage) {
    this.root = root;
    this.storage = storage;
    this.current = storage.getItem(StorageKey) === "en" ? "en" : "fa";
    this.apply();
  }

  /** Switches to the other language and remembers the choice on this device. */
  toggle(): void {
    this.current = this.current === "fa" ? "en" : "fa";
    this.storage.setItem(StorageKey, this.current);
    this.apply();
  }

  t(key: TextKey): string {
    return texts[this.current][key];
  }

  /** A clock time in the user's language (Persian digits in Persian). */
  time(date: string): string {
    return new Intl.DateTimeFormat(this.current === "fa" ? "fa-IR" : "en-GB", { hour: "2-digit", minute: "2-digit" })
      .format(new Date(date));
  }

  private apply(): void {
    this.root.lang = this.current;
    this.root.dir = this.current === "fa" ? "rtl" : "ltr";
  }
}
