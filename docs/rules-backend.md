# Reglas de Programación y Estándares para Backend (C# .NET) - Sistema Gibag

## 1. Convenciones de Nomenclatura (Naming Conventions)
* **Clases, Records, Interfaces y Métodos:** `PascalCase` (Ej: `CalculateTotal()`, `InvoiceService`, `IUserRepository`).
* **Interfaces:** Siempre deben comenzar con la letra 'I' (Ej: `ITenantContext`).
* **Variables Locales y Parámetros:** `camelCase` (Ej: `totalAmount`, `userId`).
* **Campos Privados (Private Fields):** `_camelCase` con guion bajo (Ej: `_dbContext`, `_tenantService`).
* **Constantes:** `PascalCase` (Ej: `MaxLoginAttempts`). No usar `SCREAMING_SNAKE_CASE`.

## 2. Prácticas de Código Limpio
* **Asincronía Obligatoria:** Toda operación de entrada/salida (I/O), base de datos o red debe ser asíncrona (`async`/`await`). NUNCA usar `.Result` o `.Wait()` para evitar bloqueos de hilos (Deadlocks). Los nombres de métodos no necesitan el sufijo `Async` si todo el proyecto es asíncrono por defecto, pero se debe mantener consistencia.
* **Inyección de Dependencias (DI):** Prohibido instanciar clases de servicio con la palabra `new` dentro de otros servicios (excepto DTOs, Entidades o Value Objects). Todo servicio debe inyectarse a través del constructor.
* **No usar `dynamic` ni `object`:** C# es fuertemente tipado. Usa genéricos (`<T>`) o interfaces si necesitas polimorfismo.

## 3. Acceso a Datos (Entity Framework Core)
* **Consultas de Solo Lectura:** Para cualquier consulta que no vaya a modificar datos (ej. listar productos), es OBLIGATORIO usar `.AsNoTracking()`. Esto mejora drásticamente el rendimiento.
* **Paginación:** Todos los endpoints que devuelvan listas deben estar paginados desde la base de datos usando `.Skip()` y `.Take()`. Nunca traer toda la tabla a la memoria.

## 4. Diseño de la API REST
* **Rutas (Endpoints):** Usar sustantivos en plural y `kebab-case` (Ej: `GET /api/v1/sales-orders`, `POST /api/v1/users`).
* **Respuestas Estandarizadas:** Todos los endpoints deben devolver una estructura JSON consistente. 
  * Éxito: `{ "data": { ... }, "success": true }`
  * Error: `{ "error": { "code": "ERR_01", "message": "..." }, "success": false }`
* **Códigos de Estado HTTP:** * `200 OK` (Lecturas o actualizaciones exitosas).
  * `201 Created` (Creaciones exitosas).
  * `400 Bad Request` (Errores de validación de negocio).
  * `401 Unauthorized` (Falta token JWT).
  * `403 Forbidden` (El usuario no tiene el Rol/Permiso).
  * `404 Not Found` (El recurso no existe).