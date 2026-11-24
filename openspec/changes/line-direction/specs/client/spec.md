# Spec Delta

## ADDED Requirements

### Requirement: CLI-58 Each line takes its own direction
In a message's text, each line SHALL take its direction from its own first strong letter: a Persian, Arabic or Hebrew letter makes it right to left, a Latin letter left to right. Each line SHALL line up at its own start (right for a right-to-left line). Lines inside a blockquote SHALL do the same. A code block (`pre`) SHALL stay left to right. The client SHALL NOT guess the language; it uses the Unicode bidi rule.

#### Scenario: Latin first line, then Persian
- **WHEN** a bot sends `🧵 Dalan [a37dcb87] · sms-ir` on the first line and `نیازی به تشخیص زبان نیست.` on the next
- **THEN** the first line is left to right at the left, and the second is right to left at the right, with the dot at its left end

#### Scenario: Persian first line, then Latin
- **WHEN** a message has `برنامه:` on the first line and `git push origin main` on the next
- **THEN** the first line is right to left and the second left to right

#### Scenario: Code stays left to right
- **WHEN** a message has a code block with Persian text in it
- **THEN** the code block is left to right
