using Microsoft.AspNetCore.Routing;

namespace Dalan.Telegram;

/// <summary>Plugin that turns Telegram Bot API calls into Relay contract calls. Routes come in milestone 5.</summary>
public static class TelegramAdapter
{
    public static IEndpointRouteBuilder MapTelegramAdapter(this IEndpointRouteBuilder endpoints) => endpoints;
}
