# Arquitectura, Patrones y Reglas Estrictas de Código (Sistema Gibag)

## 1. Patrón Arquitectónico Base
El proyecto backend en C# (.NET) debe construirse como un **Monolito Modular** utilizando **Clean Architecture (Arquitectura Limpia)**.

* **Regla de Dependencia Estricta:** Las dependencias fluyen siempre hacia el centro (Dominio). 
  * `Domain` no depende de nadie.
  * `Application` depende solo de `Domain`.
  * `Infrastructure` depende de `Application` y `Domain`.
  * `Presentation` (API) depende de `Application` e `Infrastructure` (solo para inyección de dependencias).

## 2. Principios de Diseño Globales
* **SOLID:** El código debe respetar estrictamente los 5 principios SOLID.
* **DRY & YAGNI:** No repetir código (Don't Repeat Yourself) y no programar funcionalidades o abstracciones que no se necesiten hoy (You Aren't Gonna Need It).
* **Patrón Result (Result Pattern):** NO utilizar excepciones para controlar el flujo normal del negocio. Los servicios y casos de uso deben retornar un objeto `Result<T>` que indique si la operación fue exitosa o fallida, y contener el error correspondiente. Las excepciones (Throw) solo se usan para errores catastróficos o no controlados del sistema.

## 3. Estructura de Solución y Carpetas
```text
Gibag.sln
│
├── 📂 src/
│   ├── 📂 Gibag.Shared/                  # (Shared Kernel) Código transversal a todo el sistema
│   │   ├── Interfaces/                   # Ej: ITenantService, ICurrentUser
│   │   ├── Exceptions/                   # Excepciones base (NotFoundException, etc.)
│   │   └── Models/                       # EntityBase, Result<T> (Patrón Result)
│   │
│   ├── 📂 Gibag.Modules.Core/            # Módulo de Empresas, Usuarios y Roles
│   │   ├── 📂 Domain/                    # Entidades (Tenant, User, Branch), Interfaces Repositorio
│   │   ├── 📂 Application/               # Casos de Uso (CQRS/Servicios), DTOs, Validadores
│   │   ├── 📂 Infrastructure/            # CoreDbContext, Implementación de Repositorios, Migraciones
│   │   └── 📂 Presentation/              # Controladores de API (CoreController)
│   │
│   ├── 📂 Gibag.Modules.Inventory/       # Módulo de Inventario
│   │   ├── 📂 Domain/                    # Entidades (Product, BranchStock)
│   │   ├── 📂 Application/               # Lógica de negocio de inventario
│   │   ├── 📂 Infrastructure/            # InventoryDbContext o configuraciones EF separadas
│   │   └── 📂 Presentation/              # Controladores de API (InventoryController)
│   │
│   ├── 📂 Gibag.Modules.Sales/           # Módulo de Ventas (POS)
│   │   └── ... (misma estructura interna Domain/Application/Infrastructure/Presentation)
│   │
│   └── 📂 Gibag.Api/                     # Punto de entrada de la aplicación
│       ├── Program.cs                    # Configuración de DI, Middlewares, Swagger
│       ├── Middlewares/                  # TenantResolutionMiddleware, GlobalExceptionHandler
│       └── appsettings.json              # Cadenas de conexión y configuración
│
└── 📂 tests/                             # Pruebas Unitarias y de Integración (Espejo de src/)