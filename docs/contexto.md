# Contexto y Arquitectura de Software: Sistema B2B SaaS "Gibag"

## 1. Visión General del Proyecto
El proyecto "Gibag" es un sistema B2B SaaS (Software as a Service) diseñado para empresas centralizadas. El objetivo principal es ofrecer una plataforma donde múltiples empresas clientes puedan registrarse y gestionar sus sucursales, inventario, ventas y empleados de manera eficiente y aislada.

## 2. Arquitectura Base
* **Modelo Multi-Tenant:** Arquitectura de Base de Datos Compartida y Esquema Compartido (Shared Database, Shared Schema).
* **Aislamiento de Datos:** Estricto. Se utilizará una columna obligatoria `tenant_id` (o `empresa_id`) en todas las tablas transaccionales y maestras. Se implementarán "Global Query Filters" en el ORM para garantizar que ninguna consulta filtre datos de otros inquilinos.
* **Patrón Arquitectónico Backend:** Monolito Modular con enfoque API-First y Clean Architecture (o Arquitectura Hexagonal). Separación clara entre la capa de Dominio, Aplicación, Infraestructura y Presentación (API).
* **Frontend:** Single Page Application (SPA) separada, con capacidades de Progressive Web App (PWA) para permitir el funcionamiento del Punto de Venta (POS) en escenarios de desconexión temporal (offline-first para ventas).

## 3. Stack Tecnológico Definido
* **Backend:** C# con .NET (versión moderna/LTS).
* **ORM:** Entity Framework Core (EF Core).
* **Base de Datos:** PostgreSQL.
* **Frontend:** React.js con TypeScript.
* **Infraestructura/Despliegue:** Contenedores Docker (Docker Compose para desarrollo local). Servicios listos para desplegarse en la nube (AWS/Azure).

## 4. Seguridad y Gestión de Accesos (RBAC)
Implementación de Control de Acceso Basado en Roles (RBAC) con autenticación basada en JWT (JSON Web Tokens).

**Jerarquía de Roles Estricta:**
1.  **Super Admin (Gibag):** Gestión global de suscripciones, empresas clientes (tenants) y métricas de la plataforma.
2.  **Admin Empresa (Tenant):** Gestión total de su propia empresa, creación de sucursales, catálogos maestros y reportes consolidados.
3.  **Gerente de Sucursal:** Acceso limitado a la gestión de inventario, empleados y métricas exclusivas de su sucursal asignada.
4.  **Operador/Cajero:** Acceso exclusivo a la interfaz de Punto de Venta (POS) y cierres de caja de su sucursal.

## 5. Módulos Iniciales (MVP)
1.  **Módulo Core/Tenancy:** Gestión de empresas, sucursales y usuarios.
2.  **Módulo de Inventario:** Catálogo de productos, control de stock por sucursal, entradas/salidas.
3.  **Módulo de Ventas (POS):** Procesamiento de ventas, carritos, métodos de pago, recibos (soporte offline/sync).
4.  **Módulo de RRHH (Básico):** Gestión de empleados, asignación a sucursales y roles.

## 6. Instrucciones y Restricciones para el Agente de Código
* **Tipado Estricto:** Utiliza tipado fuerte en C# y TypeScript en todo momento. Evita el uso de `any` en TS o `dynamic` en C#.
* **Inyección de Dependencias:** Utiliza el contenedor de inyección de dependencias nativo de .NET para todos los servicios e interfaces.
* **Filtros de Inquilino:** Al generar entidades de EF Core, asegúrate de configurar el Global Query Filter para `tenant_id` en el `OnModelCreating` del `DbContext`.
* **Controladores:** Los controladores de la API deben ser delgados (Thin Controllers) y delegar la lógica de negocio a la capa de Aplicación (Servicios o Casos de Uso/CQRS si se define).
* **Respuestas API:** Estandariza las respuestas de la API (por ejemplo, utilizando un envoltorio de respuesta consistente para éxitos y errores).