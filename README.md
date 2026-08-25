# KYC Compliance Platform

> A production-oriented, multi-tenant KYC (Know Your Customer) Compliance platform built on a modular monolith backend and micro-frontend architecture.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## Overview

This monorepo contains all applications and infrastructure configuration for a multi-tenant KYC Compliance platform used by financial institutions to onboard, verify, and monitor customers in accordance with regulatory requirements.

The platform demonstrates enterprise-grade patterns including multi-tenancy, CQRS, event sourcing, micro-frontends, and GraphQL federation — making it an ideal reference architecture for senior engineering portfolios.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Admin Shell | Angular 21+ |
| Customer Portal | React 19+ |
| Reports Dashboard | Vue 3.5+ |
| API / Backend | .NET 10, C# |
| API Protocol | Hot Chocolate GraphQL |
| Database | PostgreSQL |
| Cache / Sessions | Redis |
| Architecture | Modular Monolith + Micro-Frontends |
| Containerization | Docker / Docker Compose |

---

## Repository Structure

```
kyc-multi-frontend/
├── apps/
│   ├── angular-admin/        # Angular shell — admin & operations portal
│   ├── react-customer/       # React app — customer self-service portal
│   ├── vue-reports/          # Vue app — reporting & analytics dashboard
│   └── api/                  # .NET 10 modular monolith with GraphQL
├── docs/
│   └── business-requirements.md
├── infrastructure/
│   └── docker-compose.yml
├── .editorconfig
├── .gitignore
├── LICENSE
└── README.md
```

---

## Getting Started

> Prerequisites: Node.js 24+, .NET 10 SDK, Docker Desktop

### 1. Clone the repository

```bash
git clone https://github.com/SDS37/kyc-multi-frontend.git
cd kyc-multi-frontend
```

### 2. Start infrastructure services

```bash
docker compose -f infrastructure/docker-compose.yml up -d
```

### 3. Run the API

```bash
cd apps/api
dotnet restore
dotnet run
```

### 4. Run the Angular Admin

```bash
cd apps/angular-admin
npm install
npm start
```

### 5. Run the React Customer Portal

```bash
cd apps/react-customer
npm install
npm start
```

### 6. Run the Vue Reports Dashboard

```bash
cd apps/vue-reports
npm install
npm run dev
```

---

## Architecture

> Detailed architecture diagrams and ADRs are forthcoming in `docs/`.

### High-Level Overview

```
┌─────────────────────────────────────────────────────┐
│                   Browser / Clients                  │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────┐ │
│  │ Angular Admin│  │ React Customer│  │ Vue Reports│ │
│  └──────┬───────┘  └──────┬───────┘  └─────┬──────┘ │
└─────────┼─────────────────┼────────────────┼────────┘
          │                 │                │
          └─────────────────▼────────────────┘
                    GraphQL API Gateway
                  (Hot Chocolate / .NET 10)
                           │
          ┌────────────────┼─────────────────┐
          ▼                ▼                 ▼
      PostgreSQL         Redis           External KYC
      (primary DB)      (cache/sessions)  Providers
```

### Key Patterns

- **Multi-Tenancy**: Tenant resolution via request headers; isolated data per tenant.
- **CQRS**: Commands and Queries separated at the application layer.
- **Micro-Frontends**: Each frontend is independently deployable and developed by separate teams.
- **GraphQL**: Single schema entry point with Hot Chocolate stitching.
- **Event-Driven**: Domain events published for audit trail and downstream integrations.

---

## Documentation

- [Business Requirements](docs/business-requirements.md)

---

## License

This project is licensed under the [MIT License](LICENSE).

