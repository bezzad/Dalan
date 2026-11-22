## ADDED Requirements

### Requirement: CLI-43 Natural, formal Persian
Every Persian text the client shows SHALL be a little formal and use the common Persian word, never a literal translation: «ربات» (not «بات»), «اتصال ربات» (not «رله»), «برنامه» (not «اپ»), «برنامه‌ی رابط دالان» (not «ایجنت»), «توسعه‌دهنده» for the builder role, «مدیر» (not «ادمین»), «رمز عبور», «جدید» (not «تازه»), «طرح‌ها» (not «پلن»), «گزارش» (not «لاگ»), «پوسته» (not «تم»), «پرسش‌های متداول» and «گفت‌وگو». «می‌» and «ها» SHALL be joined with a zero-width non-joiner.

#### Scenario: No literal words
- **WHEN** every Persian text of the client (texts, landing, privacy, FAQ, both guides, the ready prompt) is read
- **THEN** none has «بات» as a word, «رله», «اپ» as a word, «ایجنت», «ادمین», «پلن», «لاگ», «تم:», «گواه کار», «محدودیت سرعت», «سوال‌های رایج» or «گفتگو»

#### Scenario: Joined prefixes
- **WHEN** every Persian text of the client is read
- **THEN** no «می» stands alone before a space
