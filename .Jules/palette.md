## 2024-05-23 - Icon-only buttons accessibility
**Learning:** Found several icon-only buttons (raw HTML and MudBlazor components) without ARIA labels, making them inaccessible to screen readers.
**Action:** Always verify `aria-label` or `title` exists on buttons that rely solely on icons for meaning.
