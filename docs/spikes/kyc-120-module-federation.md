# KYC-120 — Module Federation spike

**Result: keep three apps.** No host is on `main`. Origins stay `http://localhost:4200`, `:5173`, and `:5174`.

This spike is not W7 polish and not a Definition of Done gate ([ADR-005](../architecture-decision-records.md)). A one-URL host stays a wishlist row in [beyond-mvp.md](../beyond-mvp.md) §2. It needs a new story. KYC-120 does not authorize that work.

## Time box

The roadmap time-control rule closes this spike at the **end of W7**, the same window as polish KYC-111–115. That polish is done (2026-10-09). The spike stops the same day. It does not continue into a second build system after the window.

## What was tried

No host or remote was added to the apps. The check was whether a host could be wired onto the builders already on `main` without swapping them.

| App | Builder today | Federation package checked | Fit |
|---|---|---|---|
| Angular admin | `@angular/build` application builder `^22.1.6` (not webpack) | `@angular-architects/native-federation@22.2.2` | Peer is `@angular/build` `~22.2.0`. This repo is 22.1. Installing it means bumping the Angular build during the spike. |
| Angular admin | same | `@angular-architects/module-federation@22.0.0` | Webpack-era helper. This app is not a webpack build. |
| React customer, Vue reports | Vite `^8.2.2` | `@module-federation/vite@1.23.4` | Peer accepts Vite 5–8, so the two Vite apps could be remotes. |

A host still has to load the other two runtimes. The architecture sketch is an Angular shell. That shell is the package whose peer does not match this repo, and it would own one URL for three routers (`@angular/router`, `react-router`, `vue-router`). That is the unstable case ADR-005 told us to drop.

## What would have been shared

Only what ADR-005 already allows:

- `@kyc/design-tokens` (CSS variables)
- the GraphQL schema and JWT contract (claims, roles)

Not shared: Angular Material, React trees, Vue SFCs, or a cross-framework widget library. Each app would still ship its own framework runtime. Token storage keys are already separate (`kyc.angular-admin.*`, `kyc.react-customer.*`, `kyc.vue-reports.*`), so one origin would not overwrite another app’s token by key name. It would still share one history stack and one login URL.

## Decision

Keep the three independent apps. Do not merge a Module Federation host. Playwright smokes stay per origin. DoD does not change.
