## ADDED Requirements

### Requirement: CLI-35 What you get
The landing page SHALL have a "What you get" section after the product cards. It SHALL list four groups in this order: bot relay, users and safety, tools for builders, sites and gateway. Each group SHALL have a title, a short text and a list of things. The first three groups SHALL list only things Dalan does today; every thing in the sites and gateway group SHALL be marked "soon". Under the groups one line SHALL say what Dalan does not offer: raw TCP ports, SSH or remote desktop, and an AI gateway. No text SHALL say "tunnel".

#### Scenario: Ready and soon
- **WHEN** the groups are read in Persian or English
- **THEN** there are four, no thing in the first three is marked soon, and every thing in the fourth is marked soon

#### Scenario: Honest about what is not offered
- **WHEN** the not-offered line is read in Persian or English
- **THEN** it names TCP, SSH and the AI gateway, and no text of the section says "tunnel"
