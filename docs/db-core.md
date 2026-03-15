# Instrucciones de Base de Datos: Core y Multi-Tenancy (Sistema Gibag)

## 1. Reglas Globales y Arquitectónicas
* **Motor de Base de Datos:** PostgreSQL.
* **ORM:** Entity Framework Core (C#).
* **Llaves Primarias (PK):** Todas las tablas DEBEN usar UUID (`Guid` en C#) generados secuencialmente o mediante la base de datos (ej. `uuid-ossp` o gen_random_uuid() en Postgres) para evitar colisiones en sincronizaciones offline. NO usar enteros autoincrementables (Identity).
* **Aislamiento Multi-Tenant:** Toda tabla que pertenezca a la información de un cliente (empresa) debe incluir la columna `tenant_id` (`Guid`).
* **Seguridad ORM:** Es OBLIGATORIO configurar un `Global Query Filter` en el `OnModelCreating` del `DbContext` para cada entidad que tenga `tenant_id`. Ejemplo: `builder.Entity<Branch>().HasQueryFilter(e => e.TenantId == _currentTenantId);`.

## 2. Entidades del Núcleo (Core Schema)

### 2.1. Entidad: `Tenant` (Empresa / Cliente)
* **Descripción:** Representa a la empresa que adquiere la suscripción del SaaS.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `Name` (String 100, Required)
  * `TaxId` (String 50, Required) - Identificador fiscal.
  * `IsActive` (Bool, Default true)
  * `SubscriptionPlan` (String 50, Required)
  * `CreatedAt` (DateTime/DateTimeOffset, Required)
* **Restricciones:** Esta es la ÚNICA tabla de este módulo que NO lleva `tenant_id`.

### 2.2. Entidad: `Branch` (Sucursal)
* **Descripción:** Puntos de venta físicos o lógicos de un Tenant.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required, FK -> Tenant)
  * `Name` (String 100, Required)
  * `Address` (String 255, Required)
  * `Timezone` (String 50, Required)
  * `IsActive` (Bool, Default true)

### 2.3. Entidad: `Role` (Rol de Acceso)
* **Descripción:** Define los permisos basados en JSON para el RBAC.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required, FK -> Tenant)
  * `Name` (String 50, Required)
  * `PermissionsJson` (String/JSONB en Postgres, Required) - Contiene la matriz de permisos.

### 2.4. Entidad: `User` (Empleado / Usuario)
* **Descripción:** Credenciales y datos del personal.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required, FK -> Tenant)
  * `RoleId` (Guid, Required, FK -> Role)
  * `Email` (String 150, Required)
  * `PasswordHash` (String 255, Required)
  * `FirstName` (String 100, Required)
  * `LastName` (String 100, Required)
* **Restricciones Específicas:** Crear un **Índice Único Compuesto** en Entity Framework para `(TenantId, Email)`. Un mismo correo puede existir en la plataforma, pero no duplicado dentro de la misma empresa.

### 2.5. Entidad: `UserBranch` (Tabla Intermedia Usuario-Sucursal)
* **Descripción:** Asignación de a qué sucursales tiene acceso un empleado.
* **Propiedades:**
  * `Id` (Guid, PK)
  * `TenantId` (Guid, Required, FK -> Tenant)
  * `UserId` (Guid, Required, FK -> User)
  * `BranchId` (Guid, Required, FK -> Branch)
  * `IsPrimary` (Bool, Default false) - Indica si es la sucursal por defecto al iniciar sesión.

## 3. Instrucciones de Navegación (C#)
* Definir las propiedades de navegación (`virtual ICollection<T>` y las referencias a objetos singulares) correctamente en las clases de C# para aprovechar el Include/ThenInclude de EF Core, pero mantenerlas en un diseño limpio y sin ciclos de serialización (usar DTOs para las respuestas de la API).