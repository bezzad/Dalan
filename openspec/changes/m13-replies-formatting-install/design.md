# Design

## Context

Owner's report of 2026-11-24, checked against ArcaneBot (`ext/*.py`, its Dalan copy's log) and Telegram Bot API 10.4. Approved as "apply all" (items 1 to 5).

## Decisions

1. **A reply is a copy, not a link.** When a message replies, the content gets `ReplyTo { Seq, FromBot, Text, Date, File }`, copied from the target at that time. The bot gets a full `reply_to_message` without a lookup, a later edit of the target does not change old quotes (like Telegram), and no join is needed for history. The target must be a message of the same chat that is not deleted. Telegram's `allow_sending_without_reply` sends without the reply when the target is gone; otherwise the error is "message to be replied not found". The client shows "Deleted message" in the quote when it knows the target was deleted.
2. **Who wrote the quoted user message.** A Telegram `reply_to_message` of a user message needs `from`. In an update the adapter has the user; in the answer to `sendMessage` it has only the id, so `from` is `{id, is_bot: false, first_name: ""}`, as `chat` already has no name there.
3. **Entities are Telegram's.** `TextEntity(Type, Offset, Length, Url, Language)`, offsets and lengths in UTF-16 code units (C# string index and JavaScript string index are both UTF-16). Kinds: `bold`, `italic`, `underline`, `strikethrough`, `spoiler`, `code`, `pre`, `text_link`, `blockquote`, `expandable_blockquote`. The core checks: known kind, inside the text, length above 0, at most `Relay:MaxTextEntities`, `text_link` with an http(s) URL inside `Relay:MaxButtonUrlLength`, `language` only on `pre`, and two entities are apart or one holds the other (no partial overlap). Else `bad_entities`. Nesting lets the client build a tree, not split spans.
4. **HTML in the adapter only.** `parse_mode` is a Telegram thing, so the parser lives in the plugin. Tags as Telegram: `b strong i em u ins s strike del tg-spoiler span class="tg-spoiler" a href code pre blockquote [expandable] tg-emoji`; `<pre><code class="language-x">` gives `pre` with `language`. Character references `&lt; &gt; &amp; &quot;` and numeric ones. `<br>` is not Telegram HTML and is refused like any unknown tag: "Bad Request: can't parse entities: unsupported start tag "x" at byte offset N". A link that is not http(s) (for example `tg://user?id=`) keeps its text and drops the link. The length limit applies to the text after parsing, as in Telegram.
5. **Edits.** `IBotGateway.EditAsync(botId, userId, messageId, MessageEdit edit)`, with `MessageEdit(Text, Entities, InlineKeyboard, Part)`; `Part` is `Text` (Telegram `editMessageText`: a file message gives `no_text`), `Caption` (`editMessageCaption`: a text message gives `no_caption`), or `Any` (`/v1`: the text or the caption, whichever the message has). A text change replaces the entities; a keyboard-only edit keeps them.
6. **Switch buttons fill the input.** `InlineButton.SwitchQuery` (may be empty). Exactly one of `callback_data`, `url`, `switch_query`; the query at most `Relay:MaxTextLength`. The adapter maps all three Telegram switch fields (`switch_inline_query_chosen_chat.query`) to it and returns `switch_inline_query_current_chat`. The client puts `@username query` in the input and focuses it; the bot gets it as an ordinary message when the user sends it, which is how ArcaneBot uses it.
7. **Press state lives in the client.** The press answer (`POST .../callbacks`) gives the press id. The client marks the button `pending` until an `answer` event with that id, a newer version of that message, an error, or `press_wait_seconds`. In memory only. The server is unchanged: a second press is still allowed (the bot may want it), only the client stops double taps.
8. **One manifest per bot.** `GET /@{username}/manifest.webmanifest` needs no session and writes no access log (like the public icon). Fields: `id` and `start_url` and `scope` all `/@username`, `name` and `short_name` the bot name, `display` "standalone", the main manifest's colours and `lang`, `dir` "auto"; icons: the bot icon (`/api/bots/{id}/icon?v=N`, declared 512x512, the size the client crops to) when it has one, then Dalan's icons. 404 for an unknown, deleted or suspended bot. Cached 5 minutes. The host serves `index.html` for `/@name` with the manifest link changed to it; the client also changes the link on screen changes, because the service worker serves `/` for every navigation.
9. **Formatting is drawn with DOM nodes.** `format.ts` turns text and entities into a tree of elements with text nodes only: no `innerHTML`, so a bot cannot inject markup. Spoilers hide until a tap. An expandable quote shows three lines and opens on a tap. Links open in a new tab without referrer, like URL buttons.

## Risks

- Bots that send `parse_mode` "HTML" with a tag Telegram does not allow now get 400 instead of a raw message. That is Telegram's behaviour, so the same bot already handles it.
- A bot icon set by `setMyProfilePhoto` is not cropped, so its real size may not be 512x512; Dalan's icons follow it in the list.

## Settings

| Setting | Start | Source |
| --- | --- | --- |
| `Relay:ClientPressWaitSeconds` | 15 | Assumed (Telegram's spinner stops after about this) |
| `Relay:MaxTextEntities` | 100 | Telegram's limit |
