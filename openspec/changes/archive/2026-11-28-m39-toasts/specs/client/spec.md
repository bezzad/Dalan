## ADDED Requirements

### Requirement: CLI-97 Toasts do not pile up
A toast with the same text as one already shown SHALL replace it, so the same text is never shown twice at once. At most 3 toasts SHALL be shown at once; a new one SHALL remove the oldest.

#### Scenario: The same error on each screen
- **GIVEN** a slow network where each screen's call fails
- **WHEN** the user opens five dashboard pages in a row
- **THEN** one "Failed" toast is shown, and the menu items under the toast area can be tapped

#### Scenario: Many different toasts
- **WHEN** four different toasts come one after another
- **THEN** only the last three are shown
