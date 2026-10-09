# KYC-120 — Module Federation spike

**Result: keep three apps.** No host is on `main`. Origins stay `http://localhost:4200`, `:5173`, and `:5174`.

This spike is not W7 polish and not a Definition of Done gate ([ADR-005](../architecture-decision-records.md)). A one-URL host stays a wishlist row in [beyond-mvp.md](../beyond-mvp.md) §2. It needs a new story. KYC-120 does not authorize that work.

## Time box

The roadmap time-control rule closes this spike at the **end of W7**, the same window as polish KYC-111–115. That polish is done (2026-10-09). The spike stops the same day. It does not continue into a second build system after the window.

## What was tried

No host and no remotes were added. This is the written fail: keep three apps. The check was whether a host could be wired onto the builders already locked on `main` without moving those locks.

| App | Builder today | Federation package checked | Fit |
|---|---|---|---|
| Angular admin | `@angular/build` application builder, lockfile `22.1.6` (`package.json` says `^22.1.6`). `@angular/core` lock is `22.1.4`. Not webpack. | `@angular-architects/native-federation@22.2.2` | Peer is `@angular/build` `~22.2.0`, and the package depends on `@angular-devkit/*` `~22.2.0`. The declared `^22.1.6` range allows 22.2. The lock does not have it. Matching the peer means moving the locked Angular 22.1 set to 22.2 inside a closed spike window. |
| Angular admin | same | `@angular-architects/module-federation@22.0.0` | npm description: Webpack Module Federation with the Angular CLI. This app uses the application builder, not webpack. |
| React customer, Vue reports | Vite `^8.2.2` | `@module-federation/vite@1.23.4` | Peer accepts Vite 5–8, so the two Vite apps could be remotes. |

A host still has to load the other two runtimes. The architecture sketch is an Angular shell. That shell is the one whose lock does not match the native-federation peer, and it would own one URL for three routers (`@angular/router`, `react-router`, `vue-router`). That is the unstable case ADR-005 told us to drop.

## What would have been shared

Only what ADR-005 already allows:

- `@kyc/design-tokens` (CSS variables)
- the GraphQL schema and JWT contract (claims, roles)

Not shared: Angular Material, React trees, Vue SFCs, or a cross-framework widget library. Each app would still ship its own framework runtime. Token storage keys are already separate (`kyc.angular-admin.*`, `kyc.react-customer.*`, `kyc.vue-reports.*`), so one origin would not overwrite another app’s token by key name. It would still share one history stack and one login URL.

## Decision

Keep the three independent apps. Do not merge a Module Federation host. Playwright smokes stay per origin. DoD does not change.
