using System.Net;

namespace Dalan.Sites.Proxy;

/// <summary>Dalan's own short pages for a site that cannot answer. Persian first, then English; no file from another host.</summary>
internal static class SitePages
{
    private static readonly Dictionary<string, (string Fa, string En)> Texts = new()
    {
        ["site_offline"] = ("کامپیوتر این سایت الان خاموش است یا به دالان وصل نیست. کمی بعد دوباره امتحان کنید.",
            "The computer of this site is off or not connected to Dalan. Try again a little later."),
        ["site_not_found"] = ("سایتی با این نام در دالان نیست.", "There is no site with this name on Dalan."),
        ["site_timeout"] = ("این سایت به‌موقع جواب نداد. دوباره امتحان کنید.", "This site did not answer in time. Try again."),
        ["request_too_large"] = ("این درخواست برای این سایت خیلی بزرگ است.", "This request is too large for this site."),
        ["too_many_requests"] = ("درخواست‌های شما زیاد است. چند ثانیه صبر کنید.", "Too many requests from you. Wait a few seconds."),
        ["bad_answer"] = ("جواب این سایت درست نبود.", "This site sent an answer that is not valid."),
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
