// CLI-97: "Login with Dalan" for builders: the guide at /guide/login, the samples, and the small rules of the My apps card and
// the consent screen. Pure texts and logic, so tests run without a browser. Addresses come from the public settings (rule 4).
import type { GuideStep, Sample } from "./guide.ts";
import type { Lang, PublicNumbers } from "./pages.ts";

/** CLI-97: the addresses a site needs. `site` is the example site's own address. */
export interface LoginAddresses { issuer: string; discovery: string; site: string; callback: string }

/** The example site of the guide is always `shop` (CLI-67). */
export function loginAddresses(numbers: PublicNumbers, name = "shop"): LoginAddresses {
  const issuer = numbers.site_url.replace(/\/+$/, "");
  const domain = numbers.sites_domain || "<sites domain>";
  const site = `https://${name}.${domain}`;
  return { issuer, discovery: `${issuer}/.well-known/openid-configuration`, site, callback: `${site}/callback` };
}

/** CLI-97: the discovery address of this server, for the My apps card. */
export function discoveryUrl(siteUrl: string): string {
  return `${siteUrl.replace(/\/+$/, "")}/.well-known/openid-configuration`;
}

/** CLI-97: short samples of the OpenID Connect way, each with this server's addresses. The secret stays a placeholder. */
export function loginSamples(a: LoginAddresses): Sample[] {
  return [
    { name: "Node (openid-client)", code: `import * as client from "openid-client";

const config = await client.discovery(new URL("${a.issuer}"), "shop", process.env.DALAN_SECRET);

// Login: keep verifier and state in the visitor's session, then redirect.
const verifier = client.randomPKCECodeVerifier();
const state = client.randomState();
const url = client.buildAuthorizationUrl(config, {
  redirect_uri: "${a.callback}", scope: "openid profile",
  code_challenge: await client.calculatePKCECodeChallenge(verifier), code_challenge_method: "S256", state,
});

// Callback: the full address the visitor came back to.
const tokens = await client.authorizationCodeGrant(config, new URL(req.url, "${a.site}"),
  { pkceCodeVerifier: verifier, expectedState: state });
const user = tokens.claims(); // user.sub, user.name` },
    { name: "Python (Authlib)", code: `from authlib.integrations.flask_client import OAuth

oauth = OAuth(app)
oauth.register("dalan", client_id="shop", client_secret=os.environ["DALAN_SECRET"],
               server_metadata_url="${a.discovery}",
               client_kwargs={"scope": "openid profile", "code_challenge_method": "S256"})

@app.route("/login")
def login():
    return oauth.dalan.authorize_redirect("${a.callback}")

@app.route("/callback")
def callback():
    user = oauth.dalan.authorize_access_token()["userinfo"]  # user["sub"], user["name"]
    session["user"] = user["sub"]
    return redirect("/")` },
    { name: "PHP (jumbojett/openid-connect-php)", code: `use Jumbojett\\OpenIDConnectClient;

$oidc = new OpenIDConnectClient("${a.issuer}", "shop", getenv("DALAN_SECRET"));
$oidc->setRedirectURL("${a.callback}");
$oidc->addScope(["openid", "profile"]);
$oidc->setCodeChallengeMethod("S256");
$oidc->authenticate();            // goes to Dalan, and back here with the code
$_SESSION["user"] = $oidc->requestUserInfo("sub");` },
    { name: "PocketBase", code: `Settings > Auth providers > OpenID Connect (oidc):
  Client ID       shop
  Client secret   <the secret from My apps>
  Display name    Dalan
  Auth URL        ${a.issuer}/oauth/authorize
  Token URL       ${a.issuer}/oauth/token
  User info URL   ${a.issuer}/oauth/userinfo
  PKCE            on
Redirect URL to give: ${a.site}/api/oauth2-redirect` },
    { name: "curl (by hand)", code: `# 1. Send the visitor to:
${a.issuer}/oauth/authorize?response_type=code&client_id=shop&redirect_uri=${encodeURIComponent(a.callback)}&scope=openid%20profile&state=<random>&code_challenge=<S256 of verifier>&code_challenge_method=S256
# 2. Back at ${a.callback}?code=...&state=... swap the code:
curl -u shop:$DALAN_SECRET ${a.issuer}/oauth/token \\
  -d grant_type=authorization_code -d code=<code> -d code_verifier=<verifier> \\
  -d redirect_uri=${a.callback}` },
  ];
}

/** CLI-97: what the site's own app reads when the proxy does the login (SITE-26). */
export function proxySamples(): Sample[] {
  return [
    { name: "Node (Express)", code: `app.get("/", (req, res) => {
  const user = req.get("X-Dalan-User");                         // this site's id of the user
  const name = decodeURIComponent(req.get("X-Dalan-Name") ?? "");
  res.send(\`Hello \${name}\`);
});` },
    { name: "Python (Flask)", code: `from urllib.parse import unquote

@app.route("/")
def home():
    user = request.headers["X-Dalan-User"]
    name = unquote(request.headers.get("X-Dalan-Name", ""))
    return f"Hello {name}"` },
    { name: "PHP", code: `$user = $_SERVER["HTTP_X_DALAN_USER"];
$name = rawurldecode($_SERVER["HTTP_X_DALAN_NAME"] ?? "");
echo "Hello " . htmlspecialchars($name);` },
  ];
}

/** CLI-97: the guide at /guide/login: way 1 (the proxy, no code) and way 2 (OpenID Connect), step by step. */
export function loginGuideSteps(lang: Lang, numbers: PublicNumbers): GuideStep[] {
  const a = loginAddresses(numbers);
  const facts = `issuer          ${a.issuer}
discovery       ${a.discovery}
client_id       shop
client_secret   <from My apps>
redirect_uri    ${a.callback}   (or http://localhost:<port>/... for tests)
scope           openid profile   (+ email)
PKCE            S256, required`;
  const run = "dalan http 3000 --name shop --login";
  return lang === "fa" ? [
    { title: "یک سایت در دالان داشته باشید", text: "ابتدا سایت خود را با راهنمای سایت روی دالان آنلاین کنید. «ورود با دالان» برای سایت‌هایی است که در «برنامه‌های من» دارید؛ نمونه‌ی این راهنما سایت shop است." },
    { title: "راه ۱: ورود روی پروکسی، بدون کد", text: `ساده‌ترین راه: در «برنامه‌های من» گزینه‌ی «ورود لازم روی پروکسی» را برای سایت روشن کنید، یا برنامه‌ی رابط را با --login اجرا کنید. بازدیدکننده‌ای که وارد نشده است، به صفحه‌ی ورود دالان می‌رود و بعد به همان صفحه‌ی ${a.site} برمی‌گردد.`, code: run },
    { title: "کاربر را در برنامه‌ی خود بخوانید", text: "هر درخواست با دو سرآیند به برنامه‌ی شما می‌رسد: X-Dalan-User (شناسه‌ی کاربر، مخصوص همین سایت) و X-Dalan-Name (نام، با کدگذاری درصدی UTF-8). بازدیدکننده نمی‌تواند این سرآیندها را خودش بفرستد. خروج: /.dalan/logout روی همان سایت.", samples: proxySamples() },
    { title: "راه ۲: OpenID Connect را روشن کنید", text: "اگر خودتان ورود را در برنامه می‌سازید، در «برنامه‌های من» گزینه‌ی «ورود با دالان» را برای سایت روشن کنید. شناسه‌ی کلاینت نام سایت است. رمز کلاینت فقط یک بار نمایش داده می‌شود؛ آن را کپی کنید و بیرون از کد نگه دارید. «رمز جدید» رمز قبلی را بلافاصله از کار می‌اندازد." },
    { title: "این مقدارها را در کتابخانه‌ی خود بگذارید", text: "هر کتابخانه‌ی استاندارد OpenID Connect کار می‌کند. آدرس بازگشت باید روی آدرس خود سایت باشد.", code: facts },
    { title: "نمونه‌ی کد", text: "کتابخانه‌ی خود را انتخاب کنید:", samples: loginSamples(a) },
    { title: "حریم خصوصی کاربر", text: "سایت هرگز شماره‌ی موبایل را نمی‌گیرد. شناسه‌ی کاربر (sub) برای هر سایت فرق می‌کند. ایمیل فقط وقتی می‌آید که email را بخواهید و کاربر آن را تیک بزند. کاربر سایت‌هایی را که اجازه داده است در «ورود و امنیت» می‌بیند و می‌تواند حذف کند." },
    { title: "اگر کار نکرد", text: "error=access_denied یعنی کاربر اجازه نداد؛ error=invalid_request یعنی PKCE نیست. آدرس بازگشت نادرست در همان صفحه‌ی دالان رد می‌شود. پاسخ 400 invalid_grant از توکن یعنی رمز، کد یا verifier نادرست است یا کد قبلاً استفاده شده است. توکن تازه‌سازی (refresh) وجود ندارد؛ پس از ورود، نشست خود سایت را نگه دارید." },
  ] : [
    { title: "Have a site on Dalan", text: "First put your site online with the site guide. Login with Dalan is for the sites in \"My apps\"; this guide uses the site shop." },
    { title: "Way 1: login on the proxy, no code", text: `The easy way: in "My apps" turn on "Require login on the proxy" for the site, or run the agent with --login. A visitor who is not logged in goes to Dalan's login, then back to the same page of ${a.site}.`, code: run },
    { title: "Read the user in your app", text: "Each request reaches your app with two headers: X-Dalan-User (the user's id, only for this site) and X-Dalan-Name (the name, percent-encoded UTF-8). A visitor cannot send these headers themselves. Log out: /.dalan/logout on the same site.", samples: proxySamples() },
    { title: "Way 2: turn on OpenID Connect", text: "If your app does the login itself, turn on \"Login with Dalan\" for the site in \"My apps\". The client id is the site name. The client secret is shown only once: copy it and keep it outside your code. \"New secret\" stops the old one at once." },
    { title: "Put these values in your library", text: "Any standard OpenID Connect library works. The return address must be on the site's own address.", code: facts },
    { title: "Code samples", text: "Pick your library:", samples: loginSamples(a) },
    { title: "The user's privacy", text: "A site never gets the phone number. The user id (sub) is different for each site. The email comes only when you ask for email and the user ticks it. The user sees and removes the sites they allowed in \"Login and security\"." },
    { title: "If it does not work", text: "error=access_denied means the user said no; error=invalid_request means no PKCE. A wrong return address is refused on Dalan's own page. A 400 invalid_grant from the token step means the secret, the code or the verifier is wrong, or the code was used. There are no refresh tokens: keep your site's own session after the login." },
  ];
}

/** AUTH-41: the query of /oauth/authorize as the site sent it, for the consent screen to post. */
export function authorizeQuery(search: string): string {
  return search.startsWith("?") ? search.slice(1) : search;
}

/** AUTH-43: what the site gets, as text keys of the consent screen: always the site's own id, the name with `profile`. */
export function consentItems(scopes: readonly string[]): ("consentId" | "consentName")[] {
  return scopes.includes("profile") ? ["consentId", "consentName"] : ["consentId"];
}

/** The host of a site address, for the consent screen ("shop.dalans.ir"). */
export function hostOf(url: string): string {
  try {
    return new URL(url).host;
  } catch {
    return url;
  }
}
