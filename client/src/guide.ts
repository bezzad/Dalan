// CLI-38: the step-by-step guide at /guide, for a builder with no AI agent. Pure texts, so tests run without a browser.
// The addresses and numbers come from the public settings (rule 4), never from this file.
import { httpCommand, installCommands, serviceCommands } from "./appGuide.ts";
import { texts } from "./i18n.ts";
import type { Lang, PublicNumbers } from "./pages.ts";

export interface Sample { name: string; code: string }
/** `key`: CLI-65, the step that makes the access key in a modal. */
export interface GuideStep { title: string; text: string; code?: string; samples?: Sample[]; key?: boolean }

/** CLI-38: one line in each common bot library that points it at Dalan instead of api.telegram.org. */
export function librarySamples(api: string): Sample[] {
  return [
    { name: "python-telegram-bot", code: `from telegram.ext import Application

app = (Application.builder().token(TOKEN)
       .base_url("${api}/bot")
       .base_file_url("${api}/file/bot")
       .build())
app.run_polling()` },
    { name: "aiogram", code: `from aiogram import Bot
from aiogram.client.session.aiohttp import AiohttpSession
from aiogram.client.telegram import TelegramAPIServer

session = AiohttpSession(api=TelegramAPIServer.from_base("${api}"))
bot = Bot(TOKEN, session=session)` },
    { name: "pyTelegramBotAPI", code: `import telebot
from telebot import apihelper

apihelper.API_URL = "${api}/bot{0}/{1}"
apihelper.FILE_URL = "${api}/file/bot{0}/{1}"
bot = telebot.TeleBot(TOKEN)
bot.infinity_polling()` },
    { name: "Telegraf", code: `const { Telegraf } = require("telegraf");

const bot = new Telegraf(process.env.BOT_TOKEN, { telegram: { apiRoot: "${api}" } });
bot.launch();` },
    { name: "grammY", code: `import { Bot } from "grammy";

const bot = new Bot(process.env.BOT_TOKEN, { client: { apiRoot: "${api}" } });
bot.start();` },
    { name: "node-telegram-bot-api", code: `const TelegramBot = require("node-telegram-bot-api");

const bot = new TelegramBot(process.env.BOT_TOKEN, { polling: true, baseApiUrl: "${api}" });` },
    { name: "Telegram.Bot (.NET)", code: `using Telegram.Bot;

var bot = new TelegramBotClient(new TelegramBotClientOptions(token, baseUrl: "${api}"));` },
  ];
}

/** CLI-38: keeps the bot running on Linux after a restart or a log out, with the token outside the code. */
const service = `mkdir -p ~/.config/systemd/user
echo "BOT_TOKEN=<token>" > ~/.config/my-bot.env && chmod 600 ~/.config/my-bot.env
cat > ~/.config/systemd/user/my-bot.service <<'EOF'
[Service]
WorkingDirectory=%h/my-bot
EnvironmentFile=%h/.config/my-bot.env
ExecStart=/usr/bin/python3 bot.py
Restart=always

[Install]
WantedBy=default.target
EOF
systemctl --user enable --now my-bot
loginctl enable-linger "$USER"
journalctl --user -u my-bot -f`;

/** CLI-38: a sample in the token's real shape, so the reader sees where the token goes. */
const sampleToken = "12:AbCdEfGhIjKlMnOp";

/** The bot a guide is made for (CLI-49). */
export interface GuideBot { id: number; username: string }

/**
 * CLI-38 and CLI-49: a bot's page shows steps 5 to 9, numbered from 1, made for that bot only.
 * While a new token is on screen the test line has it; else `<id>:<TOKEN>`.
 */
export function botRunSteps(lang: Lang, numbers: PublicNumbers, n: (value: number) => string, bot: GuideBot, token: string | null): GuideStep[] {
  return guideSteps(lang, numbers, n, token, bot).slice(4);
}

export function guideSteps(lang: Lang, numbers: PublicNumbers, n: (value: number) => string, token: string | null = null, bot: GuideBot | null = null): GuideStep[] {
  const api = numbers.bot_api_url;
  const site = numbers.site_url;
  const placeholder = bot ? `${bot.id}:<TOKEN>` : sampleToken;
  const check = `curl ${api}/bot${token ?? placeholder}/getMe`;
  const testFa = token
    ? "این دستور توکن جدید شما را دارد. آن را اجرا کنید؛ پاسخ باید شامل \"ok\":true و نام بات باشد:"
    : bot
      ? `توکن کامل این بات را (که با ${bot.id}: شروع می‌شود) به جای ${placeholder} بگذارید، بدون فاصله یا <>. پاسخ باید شامل "ok":true و نام بات باشد:`
      : `توکن کامل خود را درست پس از bot بگذارید، بدون فاصله، / یا <>. در نمونه‌ی زیر، ${sampleToken} توکن است. پاسخ باید شامل "ok":true و نام بات باشد:`;
  const testEn = token
    ? "This command has your new token in it. Run it; it must answer with \"ok\":true and the bot's name:"
    : bot
      ? `Put this bot's whole token (it starts with ${bot.id}:) in place of ${placeholder}, with no space or <>. It must answer with "ok":true and the bot's name:`
      : `Put your whole token right after bot, with no space, / or <>. In this sample the token is ${sampleToken}. It must answer with "ok":true and the bot's name:`;
  const link = `${site}/@${bot?.username ?? "your_bot"}`;
  const expiry = n(numbers.update_expiry_seconds);
  return lang === "fa" ? [
    { title: "وارد شوید", text: "دکمه‌ی «ورود یا ثبت‌نام» را در بالای صفحه انتخاب کنید و با ایمیل، گیت‌هاب یا شماره‌ی موبایل وارد شوید." },
    { title: "شماره‌ی موبایل خود را تأیید کنید", text: "برای ساختن بات، شماره‌ی موبایل تأییدشده لازم است. از منوی کاربر، «حساب کاربری» را باز کنید، شماره را وارد کنید و کد پیامکی را بنویسید." },
    { title: "درخواست دسترسی توسعه‌دهنده بدهید", text: "در همان صفحه‌ی «حساب کاربری»، دکمه‌ی «درخواست دسترسی توسعه‌دهنده» را انتخاب کنید. مدیر دالان درخواست را بررسی و تأیید می‌کند. پس از تأیید، بخش «بات‌های من» فعال می‌شود." },
    { title: "بات بسازید و توکن آن را کپی کنید", text: "در «بات‌های من» نام کاربری و نام بات را وارد کنید. توکن فقط همان یک بار نمایش داده می‌شود؛ آن را کپی کنید و در جای امنی نگه دارید. اگر آن را گم کردید، «توکن جدید» را انتخاب کنید؛ توکن قبلی بلافاصله از کار می‌افتد." },
    { title: "توکن را آزمایش کنید", text: testFa, code: check },
    { title: "آدرس سرور را در کد تغییر دهید", text: `بات شما با هر کتابخانه‌ی بات تلگرام کار می‌کند. کافی است آدرس سرور را از api.telegram.org به ${api} تغییر دهید و توکن دالان را قرار دهید. بقیه‌ی کد تغییری نمی‌کند. کتابخانه‌ی خود را انتخاب کنید:`, samples: librarySamples(api) },
    { title: "بات را روی کامپیوتر خودتان اجرا کنید", text: "بات را مانند همیشه اجرا کنید. دالان فقط از long polling (getUpdates) پشتیبانی می‌کند و webhook ندارد؛ بنابراین به IP عمومی و باز کردن پورت نیازی نیست. کاربران با این لینک به بات دسترسی دارند:", code: link },
    { title: "بات را همیشه روشن نگه دارید (اختیاری)", text: "در لینوکس، این دستورها بات را پس از راه‌اندازی مجدد سیستم هم روشن نگه می‌دارند و توکن را بیرون از کد نگهداری می‌کنند. نام پوشه و دستور اجرا را با بات خود جایگزین کنید. در ویندوز و مک، برنامه را باز نگه دارید یا آن را با ابزار اجرای خودکار سیستم‌عامل اجرا کنید.", code: service },
    { title: "اگر کار نکرد", text: `پاسخ 401 یعنی توکن نادرست است یا تغییر کرده است. اگر پیام کاربر با علامت «تحویل نشد» مشخص شد، بات آن را در ${expiry} ثانیه دریافت نکرده است؛ بات باید پیوسته getUpdates را فراخوانی کند. متدی که دالان پشتیبانی نمی‌کند، پاسخ ok=false همراه با توضیح برمی‌گرداند. فهرست متدها در llms.txt آمده است.` },
  ] : [
    { title: "Log in", text: "Press \"Log in or sign up\" at the top and use your email, GitHub or mobile number." },
    { title: "Verify your mobile number", text: "Building bots needs a verified mobile number. User menu, \"Account\": give the number and type the SMS code." },
    { title: "Ask to become a builder", text: "On the same \"Account\" screen press \"Ask to become a builder\". The admin of Dalan approves the request. Then \"My bots\" opens." },
    { title: "Create a bot and copy its token", text: "In \"My bots\" give the bot a username and a name. The token is shown only this one time: copy it and keep it safe. If you lose it, press \"New token\"; the old token stops at once." },
    { title: "Test the token", text: testEn, code: check },
    { title: "Change the server address in your code", text: `Your bot works with any Telegram bot library. Change only the server address from api.telegram.org to ${api} and use the Dalan token. The rest of the code stays the same. Pick your library:`, samples: librarySamples(api) },
    { title: "Run it on your own computer", text: "Start the bot as always. Dalan has only long polling (getUpdates), no webhooks, so you need no public IP and no open port. Users reach the bot with this link:", code: link },
    { title: "Keep it running (optional)", text: "On Linux these commands keep the bot running after a restart, with the token outside the code. Change the folder and the start command to your bot's. On Windows and macOS keep the program open, or start it with the system's own start-up tool.", code: service },
    { title: "If it does not work", text: `An answer 401 means the token is wrong or was changed. If a user's message shows "not delivered", the bot did not read it within ${expiry} seconds: the bot must keep calling getUpdates. A method Dalan does not have answers ok=false with a description. The list of methods is in llms.txt.` },
  ];
}

/** CLI-49: the command line tool, ready to copy for this one bot. The access key stays a placeholder. */
export function botCliCode(site: string, bot: GuideBot): string {
  return `curl -O ${site}/dalan.mjs
export DALAN_URL=${site} DALAN_KEY=<KEY>
node dalan.mjs status ${bot.id}       # @${bot.username}: online or last seen
node dalan.mjs token ${bot.id}        # a new token; the old one stops at once
node dalan.mjs icon ${bot.id} icon.png`;
}

/** CLI-49: the MCP server for an AI tool, and a first request about this one bot. */
export function botMcpSamples(site: string, bot: GuideBot): Sample[] {
  const url = `${site}/api/mcp`;
  return [
    { name: "Claude Code", code: `claude mcp add --transport http dalan ${url} --header "Authorization: Bearer <KEY>"` },
    { name: "JSON", code: JSON.stringify({ mcpServers: { dalan: { type: "http", url, headers: { Authorization: "Bearer <KEY>" } } } }, null, 2) },
    { name: "Prompt", code: `Use the dalan MCP server. My bot is @${bot.username} (bot_id ${bot.id}). Call bot_status for it.` },
  ];
}

/** CLI-38: the site guide: become a builder, install the agent in one line, log in from "My apps", try the site, keep it online (CLI-39). */
export function siteGuideSteps(lang: Lang, numbers: PublicNumbers): GuideStep[] {
  const install = installCommands(numbers.site_url);
  const samples = [{ name: "Linux / macOS", code: install.unix }, { name: "Windows", code: install.windows }];
  const run = httpCommand([{ port: 3000, name: "mysite" }]);
  const keep = serviceCommands.install;
  return lang === "fa" ? [
    { title: "وارد شوید و دسترسی توسعه‌دهنده بگیرید", text: "وارد شوید، شماره‌ی موبایل خود را تأیید کنید و از صفحه‌ی «حساب کاربری» درخواست دسترسی توسعه‌دهنده بدهید. پس از تأیید مدیر، بخش «برنامه‌های من» در منوی کاربر فعال می‌شود." },
    { title: "برنامه‌ی رابط دالان را نصب کنید", text: "روی کامپیوتری که سایت روی آن اجرا می‌شود، این دستور را اجرا کنید:", samples },
    { title: "دسترسی سایت را با کلید ثبت کنید", text: "اگر اولین بار است سایتی به ما می‌سپاری، اول باید دسترسی سایت را با کلید ثبت کنی. «ساخت کلید» را بزنید؛ کلید همین‌جا ساخته می‌شود و دستور ورود را در ترمینال همان کامپیوتر اجرا کنید. کلید فقط یک بار نمایش داده می‌شود.", key: true },
    { title: "امتحان کنید", text: "سایت را روی کامپیوتر خود اجرا کنید (برای نمونه روی پورت ۳۰۰۰) و سپس این دستور را اجرا کنید. تا وقتی ترمینال باز است، سایت آنلاین می‌ماند و با Ctrl+C متوقف می‌شود. آدرس عمومی سایت در «برنامه‌های من» نمایش داده می‌شود.", code: run },
    { title: "سایت را همیشه آنلاین نگه دارید", text: "وقتی سایت کار کرد، دستور قبلی را با Ctrl+C متوقف کنید و این دستور را اجرا کنید. سایت در پس‌زمینه اجرا می‌شود و پس از روشن شدن دوباره‌ی کامپیوتر هم خودکار اجرا می‌شود. وضعیت را با dalan status ببینید. " + texts.fa.serviceWindowsNote, code: keep },
  ] : [
    { title: "Log in and become a builder", text: "Log in, verify your mobile number and ask to become a builder on the \"Account\" screen. After the admin approves, \"My apps\" opens from the user menu." },
    { title: "Install the Dalan agent", text: "On the computer that runs your site, run this one line:", samples },
    { title: "Register the site access with a key", text: "If this is the first time you hand us a site, first register the site access with a key. Press \"Make a key\": the key is made right here, then run the login command in a terminal on that computer. The key is shown only once.", key: true },
    { title: "Try it", text: "Start your site on your computer (for example on port 3000) and run this command. The site is online while the terminal is open; Ctrl+C stops it. The public address of the site is in \"My apps\".", code: run },
    { title: "Keep it online", text: "When it works, stop the command above with Ctrl+C and run this one. The site runs in the background and starts again after a restart. Check it with dalan status. " + texts.en.serviceWindowsNote, code: keep },
  ];
}
