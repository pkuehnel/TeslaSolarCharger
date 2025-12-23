## 2024-05-22 - MudBlazor Helper Text
**Learning:** MudBlazor inputs (TextField, NumericField, Select, etc.) support a native `HelperText` property that renders the text inside the component's container with correct styling (caption size, gray color).
**Action:** When using MudBlazor wrappers like `GenericInput`, pass the `HelperText` directly to the underlying component instead of rendering it manually with a `div` below. This ensures visual consistency and proper spacing.
