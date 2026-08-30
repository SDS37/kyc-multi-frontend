# `@kyc/design-tokens`

Shared **CSS custom properties** for color, spacing, type, and focus. Used by Angular, React, and Vue so the three apps (and a future Module Federation host/remotes) share one visual language without sharing framework components.

Full spec: [docs/ux-design-tokens.md](../../docs/ux-design-tokens.md).

## Install (workspace)

From an app that has `node_modules` at `apps/<app>/`:

```json
"@kyc/design-tokens": "file:../../packages/design-tokens"
```

Then `npm install` in that app.

## Import

```css
@import '@kyc/design-tokens/tokens.css';
```

Or add the file path to the bundler’s global styles list.

## Rules

- Prefer `var(--kyc-*)` over hard-coded colors/spacing in feature CSS.
- Map Angular Material / React / Vue component themes to these tokens — do not fork a second palette per app.
- Do not put Angular Material or Bootstrap in this package. Architecture sharing rules: [architecture.md §3](../../docs/architecture.md), [ADR-005](../../docs/architecture-decision-records.md).
