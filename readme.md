# 🏢 SGP - Point Management System (SaaS B2B)

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)
![React](https://img.shields.io/badge/React-20232A?style=for-the-badge&logo=react&logoColor=61DAFB)
![TypeScript](https://img.shields.io/badge/TypeScript-007ACC?style=for-the-badge&logo=typescript&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2CA5E0?style=for-the-badge&logo=docker&logoColor=white)

**SGP** is a B2B SaaS platform for centralized companies managing multiple branches.
It handles inventory, POS sales, and HR with strict per-tenant data isolation (Multi-Tenant).

---

## ✨ Key Features

- **Secure Multi-Tenant Architecture:** Data isolation at ORM level via `Global Query Filters`
  (shared database, shared schema).
- **Offline-First POS:** Sales module works without internet — transactions are stored locally
  via IndexedDB (`Dexie.js`) and synced in the background when connectivity is restored.
- **RBAC Access Control:** Strict permission hierarchy (Super Admin → Company Admin →
  Branch Manager → Cashier) enforced via JWT.
- **Modular Monolith:** Backend structured in independent modules (Core, Inventory, Sales)
  following Clean Architecture principles.

---

## 🛠 Tech Stack

### Backend
- **Framework:** C# .NET 10
- **Architecture:** Clean Architecture + Modular Monolith + CQRS (MediatR)
- **ORM & DB:** Entity Framework Core + PostgreSQL
- **Testing:** xUnit, FluentAssertions, Moq

### Frontend
- **Framework:** React + Vite + TypeScript
- **Styles:** Tailwind CSS
- **State & API:** TanStack React Query + Zustand + Axios
- **Offline Storage:** Dexie.js (IndexedDB)
- **Testing:** Vitest + React Testing Library

### Infrastructure
- Docker & Docker Compose (PostgreSQL + pgAdmin)

---

## 🚀 Quick Start

### Prerequisites
- .NET 10 SDK
- Node.js 20+
- Docker & Docker Compose

### Run locally

```bash
# 1. Start the database
docker-compose up -d

# 2. Run the backend
cd src/Gibag.Api
dotnet run

# 3. Run the frontend
cd frontend
npm install
npm run dev
```

- Frontend: http://localhost:5173  
- API: http://localhost:5000  
- pgAdmin: http://localhost:5050

---

## 📁 Project Structure

\```
SGP/
├── docs/                        # Architecture docs & AI context
├── docker-compose.yml           # PostgreSQL + pgAdmin
│
├── src/                         # ⚙️ BACKEND (.NET)
│   ├── Gibag.Api/               # Entry point, Middlewares, Controllers
│   ├── Gibag.Shared/            # Shared Kernel (Result pattern, Base interfaces)
│   ├── Gibag.Modules.Core/      # Auth, Users & Tenants
│   ├── Gibag.Modules.Inventory/ # Catalog & Stock
│   └── Gibag.Modules.Sales/     # POS & Sales
│
└── frontend/                    # 💻 FRONTEND (React)
    └── src/
        ├── features/            # Feature-based components & logic
        ├── shared/              # Reusable UI components
        └── services/            # Axios, Dexie & Auth config
\```
