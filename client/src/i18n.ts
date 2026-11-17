// CLI-04: Persian (right to left) first, English second. Every text is in both lists.

export const texts = {
  fa: {
    appName: "دالان",
    loginTitle: "ورود با پیامک",
    loginNote: "شماره موبایل خود را وارد کنید تا کد ورود برایتان پیامک شود.",
    devLoginTitle: "ورود آزمایشی",
    devLoginNote: "این ورود فقط برای توسعه است و پیامک نمی‌فرستد.",
    passwordLoginTitle: "ورود با رمز",
    bySms: "ورود با پیامک",
    byPassword: "ورود با رمز",
    byDevLogin: "ورود آزمایشی",
    phone: "شماره موبایل",
    name: "نام",
    login: "ورود",
    sendCode: "دریافت کد",
    preparing: "در حال آماده‌سازی…",
    codeSent: "کد ورود پیامک شد. آن را اینجا وارد کنید.",
    code: "کد ورود",
    changePhone: "تغییر شماره",
    nameNeeded: "این شماره تازه است. نام خود را هم وارد کنید.",
    loginName: "شماره موبایل یا نام کاربری",
    password: "رمز",
    account: "حساب",
    noPasswordYet: "برای ورود بدون پیامک، در بخش حساب یک رمز بگذارید.",
    username: "نام کاربری",
    usernameNote: "۵ تا ۳۲ حرف انگلیسی، عدد یا _ که با حرف شروع شود.",
    newPassword: "رمز تازه",
    currentPassword: "رمز فعلی",
    passwordNote: "رمز حداقل {n} حرف باشد. با رمز، دفعه بعد بدون پیامک وارد می‌شوید.",
    save: "ذخیره",
    saved: "ذخیره شد.",
    wrongCode: "کد درست نیست یا زمانش گذشته است. دوباره کد بگیرید.",
    smsWait: "تازه برای این شماره کد فرستاده‌ایم. کمی صبر کنید و دوباره امتحان کنید.",
    smsLimit: "امروز کدهای زیادی فرستاده شده است. با رمز وارد شوید یا فردا امتحان کنید.",
    smsUnavailable: "الان نمی‌توانیم پیامک بفرستیم. چند دقیقه بعد امتحان کنید یا با رمز وارد شوید.",
    wrongLogin: "نام کاربری، شماره یا رمز درست نیست.",
    weakPassword: "رمز خیلی کوتاه یا خیلی بلند است.",
    wrongPassword: "رمز فعلی درست نیست. اگر آن را فراموش کرده‌اید، با پیامک وارد شوید.",
    badUsername: "نام کاربری درست نیست: ۵ تا ۳۲ حرف انگلیسی، عدد یا _ که با حرف شروع شود.",
    usernameTaken: "این نام کاربری را کس دیگری گرفته است.",
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
    badPhone: "شماره موبایل را درست وارد کنید. مثال: 09121234567",
    textTooLong: "متن پیام خیلی بلند است.",
    tooFast: "کمی صبر کنید و دوباره امتحان کنید.",
    edited: "ویرایش‌شده",
    attach: "فرستادن فایل",
    commands: "دستورها",
    fileTooBig: "این فایل خیلی بزرگ است.",
    uploadLimit: "امروز فایل زیادی فرستاده‌اید. فردا دوباره امتحان کنید.",
    buttonGone: "این دکمه دیگر کار نمی‌کند.",
    failed: "انجام نشد. اتصال را بررسی کنید و دوباره امتحان کنید.",
    notDelivered: "⚠ به بات نرسید",
    online: "آنلاین",
    seenNow: "آخرین بازدید همین الان",
    seenMinutes: "آخرین بازدید {n} دقیقه پیش",
    seenHours: "آخرین بازدید {n} ساعت پیش",
    seenDate: "آخرین بازدید {n}",
    unread: "{n} پیام تازه",
    notifications: "اعلان‌ها",
    notificationsNote: "وقتی برنامه بسته است، پیام تازه بات‌ها را روی همین دستگاه خبر می‌دهیم.",
    notificationsOn: "روشن کردن اعلان‌ها",
    notificationsOff: "خاموش کردن اعلان‌ها",
    notificationsAreOn: "اعلان‌ها روی این دستگاه روشن است.",
    pushUnsupported: "این مرورگر اعلان نمی‌گیرد. در آیفون، اول دالان را با «افزودن به صفحه اصلی» نصب کنید.",
    pushBlocked: "اعلان‌ها در تنظیمات مرورگر بسته شده است. از تنظیمات مرورگر اجازه دهید.",
  },
  en: {
    appName: "Dalan",
    loginTitle: "Log in by SMS",
    loginNote: "Enter your mobile number and we will send you a login code by SMS.",
    devLoginTitle: "Development login",
    devLoginNote: "This login is for development only and sends no SMS.",
    passwordLoginTitle: "Log in with a password",
    bySms: "Log in by SMS",
    byPassword: "Log in with a password",
    byDevLogin: "Development login",
    phone: "Mobile number",
    name: "Name",
    login: "Log in",
    sendCode: "Get the code",
    preparing: "Getting ready…",
    codeSent: "The login code was sent by SMS. Enter it here.",
    code: "Login code",
    changePhone: "Change the number",
    nameNeeded: "This number is new. Enter your name too.",
    loginName: "Mobile number or username",
    password: "Password",
    account: "Account",
    noPasswordYet: "To log in without an SMS, set a password in Account.",
    username: "Username",
    usernameNote: "5 to 32 English letters, digits or _, starting with a letter.",
    newPassword: "New password",
    currentPassword: "Current password",
    passwordNote: "At least {n} characters. With a password, the next login needs no SMS.",
    save: "Save",
    saved: "Saved.",
    wrongCode: "The code is wrong or too old. Get a new code.",
    smsWait: "We just sent a code to this number. Wait a moment and try again.",
    smsLimit: "Too many codes were sent today. Log in with your password or try tomorrow.",
    smsUnavailable: "We cannot send an SMS now. Try again in a few minutes or log in with your password.",
    wrongLogin: "The username, number or password is wrong.",
    weakPassword: "The password is too short or too long.",
    wrongPassword: "The current password is wrong. If you forgot it, log in by SMS.",
    badUsername: "The username is not valid: 5 to 32 English letters, digits or _, starting with a letter.",
    usernameTaken: "Another user has this username.",
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
    badPhone: "Enter a valid mobile number. Example: 09121234567",
    textTooLong: "The message is too long.",
    tooFast: "Wait a moment and try again.",
    edited: "edited",
    attach: "Send a file",
    commands: "Commands",
    fileTooBig: "This file is too big.",
    uploadLimit: "You sent too many files today. Try again tomorrow.",
    buttonGone: "This button does not work any more.",
    failed: "That did not work. Check your connection and try again.",
    notDelivered: "⚠ not delivered",
    online: "online",
    seenNow: "last seen just now",
    seenMinutes: "last seen {n} minutes ago",
    seenHours: "last seen {n} hours ago",
    seenDate: "last seen {n}",
    unread: "{n} new messages",
    notifications: "Notifications",
    notificationsNote: "When the app is closed, we tell you on this device when a bot writes.",
    notificationsOn: "Turn on notifications",
    notificationsOff: "Turn off notifications",
    notificationsAreOn: "Notifications are on for this device.",
    pushUnsupported: "This browser cannot get notifications. On an iPhone, first add Dalan to the Home Screen.",
    pushBlocked: "Notifications are blocked in the browser settings. Allow them there.",
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

  /** A whole number in the user's language (Persian digits in Persian). */
  number(value: number): string {
    return new Intl.NumberFormat(this.current === "fa" ? "fa-IR" : "en-GB").format(value);
  }

  /** CLI-19: "online", "last seen ...", or nothing for a bot never seen. */
  seen(status: { online: boolean; last_seen: string | null }, now: Date): string {
    if (status.online) return this.t("online");
    if (!status.last_seen) return "";

    const at = new Date(status.last_seen);
    const minutes = Math.floor((now.getTime() - at.getTime()) / 60000);
    if (minutes < 1) return this.t("seenNow");
    if (minutes < 60) return this.t("seenMinutes").replace("{n}", this.number(minutes));
    if (minutes < 24 * 60) return this.t("seenHours").replace("{n}", this.number(Math.floor(minutes / 60)));
    const date = new Intl.DateTimeFormat(this.current === "fa" ? "fa-IR" : "en-GB", { dateStyle: "medium" }).format(at);
    return this.t("seenDate").replace("{n}", date);
  }

  private apply(): void {
    this.root.lang = this.current;
    this.root.dir = this.current === "fa" ? "rtl" : "ltr";
  }
}
