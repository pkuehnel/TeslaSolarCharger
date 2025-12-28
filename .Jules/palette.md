## 2024-05-24 - Accessibility: Icon-only buttons in generic components
**Learning:** Generic components that render dynamic icons (like `MudFab` in `GenericInput`) must expose a way to provide an `AriaLabel`. Without it, icon-only buttons become "mystery meat navigation" for screen reader users.
**Action:** Always verify if `StartIcon`/`EndIcon` usage in generic components is accompanied by text or an `AriaLabel` parameter. Added `PostfixButtonAriaLabel` to `GenericInput` to solve this.
