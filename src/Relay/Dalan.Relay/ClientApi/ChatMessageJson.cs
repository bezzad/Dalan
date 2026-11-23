using System.Text.Json.Serialization;
using Dalan.Relay.Chats;
using Dalan.Relay.Contracts;

namespace Dalan.Relay.ClientApi;

/// <summary>A message as a person sees it (REL-16). Its id is the sequence number in the chat.</summary>
public sealed record ChatMessageJson(
    long Id,
    long Version,
    string From,
    string Text,
    DateTimeOffset Date,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<List<string>>? ReplyKeyboard = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<List<InlineButtonJson>>? InlineKeyboard = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FileJson? File = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? Edited = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Pinned = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Deleted = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? NotDelivered = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReplyKeyboardOptionsJson? ReplyKeyboardOptions = null)
{
    public static ChatMessageJson Of(Message message) => new(
        message.Seq,
        message.Version,
        message.Direction == Direction.FromBot ? "bot" : "user",
        message.Content.Text,
        message.CreatedAt,
        message.Content.ReplyKeyboard?.ToRows(),
        message.Content.InlineKeyboard?.ToRows().ConvertAll(row => row.ConvertAll(InlineButtonJson.Of)),
        message.Content.File is { } file ? FileJson.Of(file.ToBot()) : null,
        message.EditedAt,
        message.Pinned ? true : null,
        message.Deleted ? true : null,
        message.Delivery == DeliveryState.NotDelivered && !message.Deleted ? true : null,
        message.Content.ReplyKeyboard?.ToOptions() is { } options ? ReplyKeyboardOptionsJson.Of(options) : null);
}

/// <summary>REL-17: <see cref="Styles"/> has one style or null per button, in the shape of <c>reply_keyboard</c>.</summary>
public sealed record ReplyKeyboardOptionsJson(bool Persistent, bool Resize, bool OneTime, List<List<string?>> Styles)
{
    public static ReplyKeyboardOptionsJson Of(KeyboardOptions options) =>
        new(options.Persistent, options.Resize, options.OneTime, options.Styles ?? []);
}

public sealed record InlineButtonJson(
    string Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CallbackData,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Style = null)
{
    public static InlineButtonJson Of(InlineButton button) => new(button.Text, button.CallbackData, button.Url, button.Style);

    public InlineButton ToButton() => new(Text, CallbackData, Url, Style);
}

/// <summary>A file in a message: <see cref="Type"/> is the content type.</summary>
public sealed record FileJson(
    string Id, string Kind, string Name, string Type, long Size,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Duration = null)
{
    public static FileJson Of(BotFile file) =>
        new(file.Id, file.Kind.ToString().ToLowerInvariant(), file.Name, file.ContentType, file.Size, file.Duration);
}
