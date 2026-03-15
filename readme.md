# 🏢 SGP - Sistema de Gestión de Puntos (SaaS B2B)

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet)
![React](https://img.shields.io/badge/React-20232A?style=for-the-badge&logo=react&logoColor=61DAFB)
![TypeScript](https://img.shields.io/badge/TypeScript-007ACC?style=for-the-badge&logo=typescript&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-2CA5E0?style=for-the-badge&logo=docker&logoColor=white)

**SGP (Sistema de Gestión de Puntos)** es una plataforma SaaS B2B diseñada para empresas centralizadas que necesitan administrar múltiples sucursales. Permite la gestión integral de inventario, ventas (POS) y recursos humanos con un aislamiento estricto de datos por inquilino (Multi-Tenant).

## ✨ Características Principales

* **Arquitectura Multi-Tenant Segura:** Aislamiento de datos a nivel de ORM mediante `Global Query Filters` (Base de datos compartida, Esquema compartido).
* **Punto de Venta (POS) Offline-First:** El módulo de ventas funciona incluso sin conexión a internet, almacenando transacciones localmente mediante IndexedDB (`Dexie.js`) y sincronizándolas en segundo plano al recuperar la conexión.
* **Control de Accesos (RBAC):** Jerarquía estricta de permisos (Super Admin, Admin Empresa, Gerente Sucursal, Cajero) mediante JWT.
* **Monolito Modular:** Backend estructurado en módulos independientes (Core, Inventory, Sales) siguiendo los principios de Clean Architecture.

## 🛠 Stack Tecnológico

### Backend
* **Framework:** C# .NET 10 (LTS)
* **Arquitectura:** Clean Architecture + Monolito Modular + CQRS (MediatR)
* **ORM & BD:** Entity Framework Core + PostgreSQL
* **Testing:** xUnit, FluentAssertions, Moq

### Frontend
* **Framework:** React + Vite + TypeScript
* **Estilos:** Tailwind CSS
* **Gestión de Estado y API:** TanStack React Query + Zustand + Axios
* **Almacenamiento Offline:** Dexie.js (IndexedDB)
* **Testing:** Vitest + React Testing Library

### Infraestructura
* **Contenedores:** Docker & Docker Compose (Base de datos local + pgAdmin)

## 📁 Estructura del Proyecto

El repositorio está dividido en dos aplicaciones principales y una carpeta de documentación (ADN del proyecto para agentes de IA).

```text
SGP/
├── docs/                      # Documentación arquitectónica e instrucciones (Contexto IA)
├── docker-compose.yml         # Orquestación de BD (PostgreSQL + pgAdmin)
├── SGP.slnx                   # Solución principal de .NET 10
│
├── src/                       # ⚙️ BACKEND (.NET)
│   ├── Gibag.Api/             # Punto de entrada, Middlewares y Controladores
│   ├── Gibag.Shared/          # Shared Kernel (Patrón Result, Interfaces Base)
│   ├── Gibag.Modules.Core/    # Módulo de Autenticación, Usuarios y Tenants
│   ├── Gibag.Modules.Inventory/ # Módulo de Catálogo y Stock
│   └── Gibag.Modules.Sales/   # Módulo de Ventas (POS)
│
└── frontend/                  # 💻 FRONTEND (React)
    ├── src/
    │   ├── features/          # Componentes y lógica agrupados por módulo
    │   ├── shared/            # Componentes UI reutilizables
    │   └── services/          # Configuración de Axios, Dexie y Auth
    └── package.json