## ADDED Requirements

### Requirement: CLI-35 What you get
The landing page SHALL have a "What you get" section after the product cards: four groups in order (bot relay, users and safety, tools for builders, sites and gateway), each with a title, a short text and a list. The first three SHALL list only built things; every item of the fourth SHALL be marked "soon". One line SHALL say what Dalan does not offer. No text SHALL say "tunnel".

#### Scenario: Ready and soon
- **WHEN** the groups are read in Persian or English
- **THEN** there are four, no thing in the first three is marked soon, and every thing in the fourth is marked soon

#### Scenario: Honest about what is not offered
- **WHEN** the not-offered line is read in Persian or English
- **THEN** it names TCP, SSH and the AI gateway, and no text of the section says "tunnel"
