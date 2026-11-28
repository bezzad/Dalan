## ADDED Requirements

### Requirement: SITE-25 The admin hears of each new site
Any builder MAY make a permanent site (owner, 2026-11-27: for now, with no approval). Each new site name, by a first claim (SITE-03) or a reserve (SITE-24), SHALL add a notice (NTF-12) for every admin: the site address, the builder's name and id, and a link to the builder's admin page (CLI-93), so the admin can check it and delete the site or suspend the user. A demo link (SITE-18) SHALL NOT.

#### Scenario: Reserve
- **WHEN** user 7 reserves `shop`
- **THEN** each admin's inbox has a notice with `shop.<sites domain>`, user 7's name and the link `/dashboard/admin/users/7`

#### Scenario: Same name again
- **WHEN** user 7's agent connects again with `shop`
- **THEN** no new notice is added
