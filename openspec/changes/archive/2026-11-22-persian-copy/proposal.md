# Proposal

## Why

The owner (2026-11-22): the Persian of the site reads like a translation. Words like «رله‌ی بات», «اپ», «ایجنت», «ادمین» and «پلن» are not natural for an Iranian reader, and the tone is too casual. Persian should be a bit more formal and use the common words.

## What Changes

- CLI-43 (new): Persian texts use one glossary (ربات, اتصال ربات, برنامه, برنامه‌ی رابط دالان, توسعه‌دهنده, مدیر, رمز عبور, جدید, طرح‌ها, پوسته, پرسش‌های متداول, …), a formal tone, and ZWNJ in «می‌» and «ها». A test keeps the old words out.
- Every Persian text of the client (i18n, landing, privacy, FAQ, guides, ready prompt) and the site error pages of the server is rewritten after the owner's review.
- docs/UI.md: the Persian glossary rule.

## Capabilities

### New Capabilities

- `client`: CLI-43.

## Impact

Persian texts only; no layout, behaviour, setting or package change. English text is unchanged.
