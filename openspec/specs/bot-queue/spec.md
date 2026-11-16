# bot-queue Specification

## Purpose
How a user's message waits for its bot: saved first, offered until it expires, delivered only when the bot confirms it, and never lost on a restart.

## Requirements

### Requirement: Q-01 Saved before queued
A user message SHALL be saved in the database with delivery state "pending" before its update is put in the bot's queue.

#### Scenario: A user sends a message
- **WHEN** a user's message to a bot is accepted
- **THEN** the message is in the database as "pending" and the bot's next get-updates call returns it

### Requirement: Q-02 Update expiry
An update SHALL be offered to the bot only until its expiry (setting `Relay:UpdateExpirySeconds`, start 60). An update that expires before it was handed to the bot SHALL be removed from the queue and its message marked "not delivered".

#### Scenario: Read in time
- **WHEN** the bot calls get-updates before the expiry
- **THEN** the update is returned

#### Scenario: Not read in time
- **GIVEN** an update that was never handed to the bot
- **WHEN** its expiry passes
- **THEN** the bot does not get it and its message is marked "not delivered"

#### Scenario: Bot is away
- **GIVEN** the bot makes no call
- **WHEN** the expiry of a waiting update passes and the sweep runs
- **THEN** its message is marked "not delivered" in the database

### Requirement: Q-03 Delivered only after confirm
An update handed to the bot SHALL stay in the queue until the bot confirms it by calling get-updates with an offset greater than its id. Only then SHALL the message be marked "delivered". An offset SHALL confirm only updates that were already handed to the bot. Delivery is at least once.

#### Scenario: No confirm
- **GIVEN** the bot got update 5
- **WHEN** it calls get-updates again with offset 5 or lower before the expiry
- **THEN** update 5 is returned again

#### Scenario: Confirm
- **GIVEN** the bot got update 5
- **WHEN** it calls get-updates with offset 6
- **THEN** update 5 is not returned again and its message is marked "delivered"

#### Scenario: Late confirm
- **GIVEN** the bot got update 5 before its expiry
- **WHEN** it calls get-updates with offset 6 after the expiry
- **THEN** the message is marked "delivered"

#### Scenario: Offset passes an update the bot never got
- **GIVEN** update 5 is in the queue and was never handed to the bot
- **WHEN** the bot calls get-updates with offset 9
- **THEN** update 5 is returned

#### Scenario: Handed out, not confirmed, expired
- **GIVEN** the bot got update 5 and did not confirm it
- **WHEN** it calls get-updates with offset 5 after the expiry
- **THEN** update 5 is not returned and its message is marked "not delivered"

### Requirement: Q-04 Latest command wins
While the setting `Relay:LatestCommandWins` is on (the start value), a new message from a user SHALL replace that user's older updates in the same bot's queue that were not handed to the bot yet. A replaced message SHALL be marked "not delivered".

#### Scenario: Same user sends three messages
- **WHEN** a user sends three messages before the bot reads
- **THEN** the bot gets only the last one and the first two are marked "not delivered"

#### Scenario: Different users
- **WHEN** two users each send one message before the bot reads
- **THEN** the bot gets both

#### Scenario: Already handed out
- **GIVEN** the bot got a user's update and did not confirm it yet
- **WHEN** the same user sends a new message
- **THEN** the older update stays in the queue

#### Scenario: Setting is off
- **GIVEN** `Relay:LatestCommandWins` is off
- **WHEN** a user sends three messages before the bot reads
- **THEN** the bot gets all three

### Requirement: Q-05 One long poll per bot
Only one get-updates call per bot SHALL wait at a time. A new call SHALL replace the waiting one, and the replaced call SHALL end with HTTP 409 and error code `conflict`.

#### Scenario: Second call while the first waits
- **GIVEN** a get-updates call of a bot is waiting
- **WHEN** a second get-updates call arrives with the same token
- **THEN** the first call ends with 409 `conflict` and the second call waits

### Requirement: Q-06 Long polling
A get-updates call SHALL return at once when updates are waiting. With an empty queue it SHALL wait until an update arrives or the timeout passes, then return what it has (maybe nothing). The timeout is the `timeout` parameter in seconds; without it the default applies (`Relay:PollTimeoutDefaultSeconds`, start 30); a value above the maximum (`Relay:PollTimeoutMaxSeconds`, start 50) is cut to the maximum.

#### Scenario: Update is waiting
- **WHEN** the bot calls get-updates and the queue has an update
- **THEN** the call returns it at once

#### Scenario: Update arrives during the wait
- **GIVEN** a get-updates call is waiting
- **WHEN** a user sends a message
- **THEN** the call returns that update at once

#### Scenario: Nothing arrives
- **WHEN** the timeout passes with an empty queue
- **THEN** the call returns 200 with an empty list

#### Scenario: Timeout zero
- **WHEN** the bot calls get-updates with `timeout=0` and the queue is empty
- **THEN** the call returns an empty list at once

#### Scenario: Timeout not given or too big
- **WHEN** the call has no `timeout`, or one above the maximum
- **THEN** the default, or the maximum, is used

### Requirement: Q-07 Queue survives a restart
On start the server SHALL put every "pending" user message that is not expired back in its bot's queue, and SHALL mark every expired "pending" message "not delivered".

#### Scenario: Restart with a waiting message
- **GIVEN** a "pending" message younger than the expiry
- **WHEN** the server restarts
- **THEN** the bot's next get-updates call returns it with the same update id

#### Scenario: Restart with an expired message
- **GIVEN** a "pending" message older than the expiry
- **WHEN** the server restarts
- **THEN** the bot does not get it and it is marked "not delivered"

### Requirement: Q-08 Order and batch size
A get-updates call SHALL return updates in rising update id order, and no more than `limit` of them. Without `limit`, or with one above the maximum, the maximum applies (`Relay:MaxUpdatesPerPoll`, start 100).

#### Scenario: More updates than the limit
- **GIVEN** five updates are waiting
- **WHEN** the bot calls get-updates with `limit=2`
- **THEN** it gets the two with the lowest ids, lowest first

#### Scenario: Limit too big
- **WHEN** the bot calls get-updates with a `limit` above the maximum
- **THEN** it gets at most the maximum

### Requirement: Q-09 Stopping ends waiting polls
When the server starts to stop, every waiting get-updates call SHALL return at once with 200 and an empty list.

#### Scenario: Deploy during a long poll
- **GIVEN** a get-updates call is waiting
- **WHEN** the server starts to stop
- **THEN** the call returns 200 with an empty list without waiting for its timeout
