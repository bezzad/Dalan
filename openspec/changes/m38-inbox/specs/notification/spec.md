## ADDED Requirements

### Requirement: NTF-12 Inbox
Every user SHALL have an inbox of notices (title, short text, an optional link on this site, time, read). Other services SHALL add one through `IInbox` (Notification.Contracts). `GET /api/inbox?before=<id>` SHALL list the user's own, newest first, `Notification:InboxPageSize` (start 30) a page, with `unread`; `POST /api/inbox/read` `{"up_to"}` SHALL mark them read. Notices older than `Notification:InboxDays` (start 90) SHALL be removed. Each call writes the access log.

#### Scenario: A notice
- **GIVEN** a notice for Sara
- **WHEN** Sara lists her inbox, then marks it read
- **THEN** the list has it with `read` false and `unread` 1, then `unread` 0

#### Scenario: Only one's own
- **WHEN** Ali lists his inbox
- **THEN** Sara's notices are not in it

### Requirement: NTF-13 A notice reaches the phone
Each new notice SHALL be pushed to the user's devices (NTF-06) with its title and link. When the user has no push subscription, or every push of it fails (the push services may be out of reach when the links abroad are cut), the user's phone SHALL get an SMS: the title and the link, at most `Notification:InboxSmsLength` (start 120) characters, and at most `Notification:InboxSmsPerDay` (start 5) a day. A user with no phone gets no SMS. Each SMS is recorded like every sent code (ADM-04), with the server's own address (127.0.0.1) as the asker. The SMS goes through `ISmsSender` (NTF-01); `smsir` sends it with the same verify call as a code (NTF-02), with the template `Notification:SmsIr:NoticeTemplateId` and its one parameter `Notification:SmsIr:NoticeParameter` holding the text; template 0 sends no notice SMS.

#### Scenario: Push works
- **GIVEN** Sara has one push device that accepts
- **WHEN** a notice is added for her
- **THEN** it is pushed and no SMS is sent

#### Scenario: Push cannot reach
- **GIVEN** every push service refuses or times out
- **WHEN** a notice is added for Sara, who has a phone
- **THEN** one short SMS goes to her phone and is recorded

#### Scenario: Daily limit
- **GIVEN** Sara got `Notification:InboxSmsPerDay` SMS notices today
- **WHEN** another notice cannot be pushed
- **THEN** no SMS is sent; the notice is still in the inbox
