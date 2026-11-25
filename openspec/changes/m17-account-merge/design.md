# Design

- **Proof before reveal.** The server says a phone, email or GitHub account belongs to another account only after the proof (right SMS code, right email code, GitHub callback). So a logged-in user cannot test numbers to find accounts.
- **Pending merge.** After the proof the server stores one pending merge per session in `identity.pending_merges` (`session_id` unique, `user_id`, `other_user_id`, `way`, `expires_at`; expiry `Identity:MergeSeconds`, start 600). The client never holds a merge secret: `GET /api/account/merge` shows it, `POST /api/account/merge` does it, `DELETE /api/account/merge` drops it. A new proof replaces it.
- **Products.** Not deleted bots (Relay), sites and demos (Sites), access keys (Identity). Identity asks Relay and Sites through their `.Contracts` (`IUserProducts.CountAsync(userId)`), right before the merge.
- **The merge transaction (Identity).** One transaction: check the pending merge and the fields again; move the empty fields to the kept user; delete the other user (its sessions and keys go by cascade); write the audit row; put `UserMerged(from, into, at)` in the outbox. A bot made by the other account in the moment between the product check and the transaction is not lost: Relay also moves not deleted bots on `UserMerged` (there should be none).
- **Chats (Relay, on `UserMerged`).** Per bot:
  - Only the gone user has a chat: change `user_id` on the membership, its messages and files. Ids stay.
  - Both have a chat: order both message lists by `date`, then the kept user first, then old id. New ids start after the largest old id of both chats (`max + 1`, `max + 2`, ...), versions the same way. So an old message id never points to another message: a bot that edits or replies to an old id gets "message not found". Read position: the new id of the last read message of the kept chat. The gone membership is deleted. A live `chat_reset` event makes the kept user's clients load the chat again.
  - In both cases `relay.moved_chats (bot_id, from_user_id, to_user_id, at)` gets a row, and the bot queue gets one `chat_moved` update.
  - `moved_chats` rows live `Relay:MovedChatDays` (start 180); after that a send to the old id is just "chat not found".
- **Push subscriptions (Notification, on `UserMerged`).** Move to the kept user; beyond `MaxSubscriptionsPerUser` the oldest go.
- **Logs.** `admin.access_log` rows are never changed (rule 13): the gone id stays in old rows. The audit row is `user {from} merged into user {into}` with the way.
- **New settings:** `Identity:MergeSeconds` = 600, `Relay:MovedChatDays` = 180.
