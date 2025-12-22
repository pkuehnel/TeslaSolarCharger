## 2025-05-24 - Missing ARIA Labels on Icon-Only Buttons
**Learning:** The application uses `MudIconButton` extensively for actions like "Delete" and "Info" but consistently lacks `AriaLabel` properties. This makes these critical actions inaccessible to screen reader users, who only hear "button" without context.
**Action:** When using `MudIconButton`, always include an `AriaLabel` bound to a localized string describing the action (e.g., "Delete", "Information"). Add `GeneralDelete` to `TranslationKeys` to support this pattern globally.
