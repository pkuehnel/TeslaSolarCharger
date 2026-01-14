## 2026-01-14 - Accessible Icon-Only Buttons in Generic Components
**Learning:** Generic input components with optional action buttons (like `GenericInput`) must expose a parameter for the button's accessible label (aria-label/tooltip). Without it, consumers cannot provide context for screen readers or hover states.
**Action:** When designing reusable components with icon buttons, always include a `Label` or `TooltipText` parameter and ensure it propagates to `aria-label` and `MudTooltip`.
