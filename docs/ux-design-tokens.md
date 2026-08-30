# UX design tokens & accessibility

Shared visual and a11y baseline for **all** KYC frontends (`apps/angular-admin`, `apps/react-customer`, `apps/vue-reports`). Implementation lives in [`packages/design-tokens`](../packages/design-tokens/) as CSS custom properties so tokens stay **framework-agnostic** and still work if Module Federation is introduced later (ADR-005).

**Not this file:** how to write Angular/React/Vue code ([frontend-code-standards.md](frontend-code-standards.md)), API contracts (ADRs / API README).

## Why tokens (not a shared component library)

| Share | Do not share (MVP) |
|---|---|
| Colors, spacing, type, radius, focus ring (`--kyc-*`) | Angular Material components into React/Vue |
| Short a11y rules (WCAG / WAI-ARIA) | One mega UI kit across frameworks |

Each app owns its UI toolkit (Material on Angular; later libraries on React/Vue) but **themes and spacing map to the same tokens**.

## Package

- CSS: [`packages/design-tokens/tokens.css`](../packages/design-tokens/tokens.css)
- npm name: `@kyc/design-tokens` (`file:` dependency from each app)

### Color

Slate neutrals + teal brand (compliance-admin look). Avoid purple-on-white and cream/terracotta “AI default” palettes.

| Token | Role |
|---|---|
| `--kyc-color-brand` / `-hover` / `-on-brand` | Primary actions |
| `--kyc-color-surface` / `-raised` / `-border` | Page / card / rules |
| `--kyc-color-text` / `-muted` | Body / secondary |
| `--kyc-color-danger` / `-success` / `-warning` (+ `-bg`) | Semantic feedback |
| `--kyc-color-focus` | Focus ring base |

### Spacing & layout

Scale `--kyc-space-1` … `--kyc-space-8` (4px base). Page gutter: `--kyc-page-gutter`. Content width: `--kyc-content-max`. Prefer these over ad-hoc `margin: 13px`.

### Type & radius

`--kyc-font-sans`, `--kyc-text-*`, `--kyc-radius-*`. Mono for IDs/URLs: `--kyc-font-mono`.

### Focus

`--kyc-focus-ring` — visible keyboard focus is required. The token has a non-`color-mix` fallback plus an `@supports` enhancement. Apps should keep a real `outline` (not `outline: none` alone) and handle `forced-colors: active` so high-contrast modes still show focus (WCAG 2.4.7 / 2.4.11).

## Accessibility (all three frameworks)

Align on **WAI-ARIA** and **WCAG 2.2 AA** intent for MVP screens (login, case list, review). Angular’s ARIA helpers, React `aria-*`, and Vue `aria-*` all target the **same** platform APIs — do not invent three different a11y models.

| Rule | Practice |
|---|---|
| Labels | Every input has a visible `<label>` or `aria-label` / `aria-labelledby` |
| Errors | Associate messages with fields (`aria-describedby` / Material error state); use `--kyc-color-danger` + text, not color alone |
| Keyboard | Login and primary actions usable without a pointer; focus order matches reading order |
| Focus visible | Use `--kyc-focus-ring` or Material/CDK defaults mapped to tokens |
| Contrast | Brand/text/danger on surfaces should meet AA for normal text where practical |
| Live updates | Prefer polite `aria-live` for async login errors |

### Framework hooks

| App | Prefer |
|---|---|
| Angular | Angular Material + CDK a11y; Angular ARIA APIs where helpful; semantic HTML first |
| React | Native `aria-*` / roles; `eslint-plugin-jsx-a11y` when the app is scaffolded |
| Vue | Native `aria-*` on templates; vue-eslint-plugin accessibility rules when scaffolded |

Guards and UI redirects are **UX**, not security (JWT still enforced by the API).

## Micro frontends later

If a shell loads remotes (Week 7 spike), either:

1. Host loads `@kyc/design-tokens/tokens.css` once, or  
2. Each remote imports the same file (variables redefine identically — safe).

Do **not** share compiled Angular/React/Vue component bundles as the design system. Architecture diagram and table: [architecture.md §3](architecture.md).

## Links

- [frontend-code-standards.md](frontend-code-standards.md)
- [ADR-005](architecture-decision-records.md) (independent apps / MF deferred)
- [WAI-ARIA](https://www.w3.org/WAI/ARIA/apg/)
- [WCAG 2.2](https://www.w3.org/WAI/WCAG22/quickref/)
- Angular Material theming (map to `--kyc-*` in KYC-061)
