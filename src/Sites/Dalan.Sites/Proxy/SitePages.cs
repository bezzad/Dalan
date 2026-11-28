using System.Net;

namespace Dalan.Sites.Proxy;

/// <summary>Dalan's own short pages for a site that cannot answer. Persian first, then English; no file from another host.</summary>
internal static class SitePages
{
    private static readonly Dictionary<string, (string Fa, string En)> Texts = new()
    {
        ["site_offline"] = ("کامپیوتر این سایت در حال حاضر خاموش است یا به دالان متصل نیست. کمی بعد دوباره تلاش کنید.",
            "The computer of this site is off or not connected to Dalan. Try again a little later."),
        ["site_not_found"] = ("سایتی با این نام در دالان وجود ندارد.", "There is no site with this name on Dalan."),
        ["site_timeout"] = ("این سایت به‌موقع پاسخ نداد. دوباره تلاش کنید.", "This site did not answer in time. Try again."),
        ["request_too_large"] = ("حجم این درخواست برای این سایت بیش از حد مجاز است.", "This request is too large for this site."),
        ["too_many_requests"] = ("تعداد درخواست‌های شما زیاد است. چند ثانیه صبر کنید.", "Too many requests from you. Wait a few seconds."),
        ["demo_ended"] = ("مهلت این دمو به پایان رسیده است.", "This demo has ended."),
        ["demo_not_found"] = ("دمویی با این آدرس در دالان وجود ندارد.", "There is no demo at this address on Dalan."),
        ["bad_answer"] = ("پاسخ این سایت معتبر نبود.", "This site sent an answer that is not valid."),
        ["login_required"] = ("این سایت ورود با دالان را لازم دارد. صفحه را دوباره باز کنید تا وارد شوید.",
            "This site needs you to log in with Dalan. Open the page again to log in."),
        ["login_failed"] = ("ورود کامل نشد یا مهلت آن تمام شد. صفحه‌ی سایت را دوباره باز کنید.",
            "The login did not finish, or it took too long. Open the site's page again."),
        ["login_denied"] = ("شما اجازه ندادید؛ بدون ورود نمی‌توانید این سایت را ببینید.", "You did not allow it. This site needs a login."),
        ["logged_out"] = ("از این سایت خارج شدید.", "You logged out of this site."),
    };

    public static string Render(string code)
    {
        var (fa, en) = Texts[code];
        return $$"""
            <!doctype html>
            <html lang="fa" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Dalan</title>
            <style>body{font-family:system-ui,sans-serif;background:#0b0d10;color:#e6e8eb;display:grid;place-items:center;min-height:100vh;margin:0;padding:16px;text-align:center}p{max-width:32rem;line-height:1.8}small{color:#8a9099}</style>
            </head><body><main><p>{{WebUtility.HtmlEncode(fa)}}</p><p dir="ltr">{{WebUtility.HtmlEncode(en)}}</p><small dir="ltr">{{code}} · Dalan</small></main></body></html>
            """;
    }
}
