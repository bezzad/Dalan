## ADDED Requirements

### Requirement: CLI-76 Readable small text on public pages
On public pages, no text other than code SHALL be smaller than `--text-small`: 13px in English, 14px in Persian. Section labels (`.label`: the line above a section title, product labels, footer column titles) SHALL use `--text-label`: 14px in English and 15px in Persian. Both sizes SHALL be tokens in `:root`, set again under `:lang(fa)`.

#### Scenario: Persian section label
- **WHEN** the landing page is shown in Persian
- **THEN** the label "امکانات" is 15px, in Vazirmatn, with no letter spacing

#### Scenario: English section label
- **WHEN** the landing page is shown in English
- **THEN** the section labels are 14px, uppercase

#### Scenario: Nothing below the floor
- **WHEN** the CSS rules used by public pages are read (code blocks aside)
- **THEN** no font size is below `--text-small`, and `.label`, `.frame-label`, `ul.guards`, `.branch .box`, `.tag` and `.soon` use the tokens
