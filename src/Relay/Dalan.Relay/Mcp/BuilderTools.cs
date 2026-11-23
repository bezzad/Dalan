using System.ComponentModel;
using System.Text.Json;
using Dalan.Relay.Bots;
using Dalan.Relay.Builder;
using Dalan.Relay.ClientApi;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Dalan.Relay.Mcp;

/// <summary>
/// REL-46: the MCP tools of a builder. Each one calls <see cref="BuilderBots"/>, like its REST call, and gives the same
/// JSON as text. A refused call is a tool error with the same message, so an agent can fix it and try again.
/// </summary>
[McpServerToolType]
internal static class BuilderTools
{
    [McpServerTool(Name = "list_bots", ReadOnly = true, OpenWorld = false)]
    [Description("Lists your Dalan bots with id, username, name, online, last_seen and suspended.")]
    public static Task<CallToolResult> ListBotsAsync(BuilderBots bots, IHttpContextAccessor http, CancellationToken cancellationToken) =>
        AnswerAsync(bots.ListAsync(UserId(http), cancellationToken));

    [McpServerTool(Name = "create_bot", OpenWorld = false)]
    [Description("Creates a Dalan bot and returns its id and token. Keep the token secret: it is shown only now.")]
    public static Task<CallToolResult> CreateBotAsync(
        BuilderBots bots,
        IHttpContextAccessor http,
        [Description("5 to 32 characters of a-z, 0-9 and _, starting with a letter, for example weather_bot.")] string username,
        [Description("The name people see, for example Weather.")] string name,
        CancellationToken cancellationToken) =>
        AnswerAsync(bots.CreateAsync(UserId(http), new CreateBotRequest(username, name), cancellationToken));

    [McpServerTool(Name = "rotate_token", Destructive = true, OpenWorld = false)]
    [Description("Gives a bot a new token. The old token stops working at once.")]
    public static Task<CallToolResult> RotateTokenAsync(
        BuilderBots bots, IHttpContextAccessor http, [Description("The bot id from list_bots.")] long bot_id,
        CancellationToken cancellationToken) =>
        AnswerAsync(bots.RotateAsync(UserId(http), bot_id, cancellationToken));

    [McpServerTool(Name = "bot_status", ReadOnly = true, OpenWorld = false)]
    [Description("Says whether a bot is online (it polled for updates in the last minute) and when it was last seen.")]
    public static Task<CallToolResult> BotStatusAsync(
        BuilderBots bots, IHttpContextAccessor http, [Description("The bot id from list_bots.")] long bot_id,
        CancellationToken cancellationToken) =>
        AnswerAsync(bots.GetAsync(UserId(http), bot_id, cancellationToken));

    [McpServerTool(Name = "set_bot_icon", OpenWorld = false)]
    [Description("Sets the icon (profile picture) of a bot: a PNG, JPEG or WebP image, square, small. Gives the bot with its icon_version.")]
    public static async Task<CallToolResult> SetBotIconAsync(
        BuilderBots bots, BotIcons icons, IHttpContextAccessor http, [Description("The bot id from list_bots.")] long bot_id,
        [Description("The image file, base64 encoded.")] string image_base64, CancellationToken cancellationToken)
    {
        // REL-51: a text too long for the limit is refused before any bytes are made from it.
        if (image_base64.Length > (icons.MaxBytes + 2) / 3 * 4 + 4)
        {
            return await AnswerAsync(Task.FromResult(BuilderAnswer.Error(StatusCodes.Status400BadRequest, "bad_icon",
                "The icon is too big. Send a smaller PNG, JPEG or WebP image.")));
        }

        var bytes = new byte[image_base64.Length * 3 / 4 + 3];
        if (!Convert.TryFromBase64String(image_base64.Trim(), bytes, out var written))
        {
            return await AnswerAsync(Task.FromResult(BuilderAnswer.Error(StatusCodes.Status400BadRequest, "bad_icon",
                "'image_base64' is not base64. Send the bytes of a PNG, JPEG or WebP file as base64 text.")));
        }

        using var content = new MemoryStream(bytes, 0, written);
        return await AnswerAsync(bots.SetIconAsync(UserId(http), bot_id, content, cancellationToken));
    }

    private static long UserId(IHttpContextAccessor http) => ClientApiFilter.UserId(http.HttpContext!);

    private static async Task<CallToolResult> AnswerAsync(Task<BuilderAnswer> call)
    {
        var answer = await call;
        var text = answer.Body is ErrorJson error ? error.Message : JsonSerializer.Serialize(answer.Body, ApiJson.Options);
        return new CallToolResult { IsError = answer.Body is ErrorJson, Content = [new TextContentBlock { Text = text }] };
    }
}
