// CLI-38: the step-by-step guide at /guide, for a builder with no AI agent. Pure texts, so tests run without a browser.
// The addresses and numbers come from the public settings (rule 4), never from this file.
import { httpCommand, installCommands } from "./appGuide.ts";
import type { Lang, PublicNumbers } from "./pages.ts";

export interface Sample { name: string; code: string }
export interface GuideStep { title: string; text: string; code?: string; samples?: Sample[] }

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

export function guideSteps(lang: Lang, numbers: PublicNumbers, n: (value: number) => string): GuideStep[] {
  const api = numbers.bot_api_url;
  const site = numbers.site_url;
  const check = `curl ${api}/bot<token>/getMe`;
  const link = `${site}/@your_bot`;
  const expiry = n(numbers.update_expiry_seconds);
  return lang === "fa" ? [
    { title: "وارد شوید", text: "دکمه‌ی «ورود یا ثبت‌نام» بالای صفحه را بزنید و با ایمیل، GitHub یا شماره موبایل وارد شوید." },
    { title: "شماره موبایل را تأیید کنید", text: "برای ساختن بات، شماره موبایل تأییدشده لازم است. منوی کاربر، «حساب»، شماره را بدهید و کد پیامک را بزنید." },
    { title: "درخواست سازنده شدن بدهید", text: "در همان صفحه‌ی «حساب» دکمه‌ی «درخواست سازنده شدن» را بزنید. ادمین دالان درخواست را تأیید می‌کند. بعد از تأیید، «بات‌های من» باز می‌شود." },
    { title: "بات بسازید و توکن را بردارید", text: "در «بات‌های من» نام کاربری و نام بات را بدهید. توکن فقط همان یک بار نشان داده می‌شود: کپی کنید و جای امنی نگه دارید. اگر گم شد، «توکن تازه» بزنید؛ توکن قبلی همان لحظه از کار می‌افتد." },
    { title: "توکن را امتحان کنید", text: "این دستور باید جوابی با \"ok\":true و نام بات بدهد:", code: check },
    { title: "آدرس سرور را در کد عوض کنید", text: `بات شما با هر کتابخانه‌ی بات تلگرام کار می‌کند. فقط آدرس سرور را از api.telegram.org به ${api} عوض کنید و توکن دالان را بگذارید. بقیه‌ی کد همان است. کتابخانه‌ی خودتان را انتخاب کنید:`, samples: librarySamples(api) },
    { title: "روی کامپیوتر خودتان اجرا کنید", text: "بات را مثل همیشه اجرا کنید. دالان فقط long polling (getUpdates) دارد و webhook ندارد، پس IP عمومی و پورت باز لازم نیست. کاربران با این لینک به بات می‌رسند:", code: link },
    { title: "بات را روشن نگه دارید (اختیاری)", text: "روی لینوکس، این دستورها بات را بعد از ری‌استارت هم روشن نگه می‌دارند و توکن را بیرون از کد نگه می‌دارند. پوشه و دستور اجرا را با بات خودتان عوض کنید. روی ویندوز و مک، برنامه را باز نگه دارید یا با ابزار شروع خودکار سیستم اجرا کنید.", code: service },
    { title: "اگر کار نکرد", text: `جواب 401 یعنی توکن اشتباه است یا عوض شده. اگر پیام کاربر «نرسید» شد، بات در ${expiry} ثانیه آن را نخوانده است: بات باید همیشه getUpdates بزند. متدی که دالان ندارد جواب ok=false با توضیح می‌دهد. فهرست متدها در llms.txt است.` },
  ] : [
    { title: "Log in", text: "Press \"Log in or sign up\" at the top and use your email, GitHub or mobile number." },
    { title: "Verify your mobile number", text: "Building bots needs a verified mobile number. User menu, \"Account\": give the number and type the SMS code." },
    { title: "Ask to become a builder", text: "On the same \"Account\" screen press \"Ask to become a builder\". The admin of Dalan approves the request. Then \"My bots\" opens." },
    { title: "Create a bot and copy its token", text: "In \"My bots\" give the bot a username and a name. The token is shown only this one time: copy it and keep it safe. If you lose it, press \"New token\"; the old token stops at once." },
    { title: "Test the token", text: "This command must answer with \"ok\":true and the bot's name:", code: check },
    { title: "Change the server address in your code", text: `Your bot works with any Telegram bot library. Change only the server address from api.telegram.org to ${api} and use the Dalan token. The rest of the code stays the same. Pick your library:`, samples: librarySamples(api) },
    { title: "Run it on your own computer", text: "Start the bot as always. Dalan has only long polling (getUpdates), no webhooks, so you need no public IP and no open port. Users reach the bot with this link:", code: link },
    { title: "Keep it running (optional)", text: "On Linux these commands keep the bot running after a restart, with the token outside the code. Change the folder and the start command to your bot's. On Windows and macOS keep the program open, or start it with the system's own start-up tool.", code: service },
    { title: "If it does not work", text: `An answer 401 means the token is wrong or was changed. If a user's message shows "not delivered", the bot did not read it within ${expiry} seconds: the bot must keep calling getUpdates. A method Dalan does not have answers ok=false with a description. The list of methods is in llms.txt.` },
  ];
}

/** CLI-38: the site guide: become a builder, install the agent in one line, log in from "My apps", run the site (CLI-39). */
export function siteGuideSteps(lang: Lang, numbers: PublicNumbers): GuideStep[] {
  const install = installCommands(numbers.site_url);
  const samples = [{ name: "Linux / macOS", code: install.unix }, { name: "Windows", code: install.windows }];
  const run = httpCommand([{ port: 3000, name: "mysite" }]);
  return lang === "fa" ? [
    { title: "وارد شوید و سازنده شوید", text: "وارد شوید، شماره موبایل را تأیید کنید و در صفحه‌ی «حساب» درخواست سازنده شدن بدهید. بعد از تأیید ادمین، «اپ‌های من» در منوی کاربر باز می‌شود." },
    { title: "برنامه‌ی دالان را نصب کنید", text: "روی کامپیوتری که سایت روی آن است، این یک خط را اجرا کنید:", samples },
    { title: "یک بار وارد شوید", text: "در «اپ‌های من» دکمه‌ی «ساخت کلید و دستور ورود» را بزنید و خطی را که نشان می‌دهد اجرا کنید. کلید فقط همان یک بار دیده می‌شود." },
    { title: "سایت را آنلاین کنید", text: "سایت را روی کامپیوترتان روشن کنید (مثلاً روی پورت ۳۰۰۰) و این دستور را بزنید. آدرس عمومی سایت در «اپ‌های من» دیده می‌شود.", code: run },
  ] : [
    { title: "Log in and become a builder", text: "Log in, verify your mobile number and ask to become a builder on the \"Account\" screen. After the admin approves, \"My apps\" opens from the user menu." },
    { title: "Install the Dalan agent", text: "On the computer that runs your site, run this one line:", samples },
    { title: "Log in once", text: "In \"My apps\" press \"Make a key and login command\", then run the line it shows. The key is shown only once." },
    { title: "Put your site online", text: "Start your site on your computer (for example on port 3000) and run this command. The public address of the site is in \"My apps\".", code: run },
  ];
}
